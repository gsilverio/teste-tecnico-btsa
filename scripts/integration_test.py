"""Integration against docker-compose.integration.yaml only (disposable database)."""
import base64
import concurrent.futures
import datetime as dt
import json
import subprocess
import time
import urllib.error
import urllib.request
import urllib.parse
import uuid

API = 'http://127.0.0.1:25154'
WEB = 'http://127.0.0.1:23001'
BROKER = 'http://127.0.0.1:35673'
PG = 'btsa-integration-postgres-1'
DB = 'btsa_e2e_test'
COMPOSE = ['docker', 'compose', '-p', 'btsa-integration', '-f', 'docker-compose.yaml', '-f', 'docker-compose.integration.yaml']
AUTH = 'Basic ' + base64.b64encode(b'btsa:btsa-test').decode()


def http(path, body=None, base=API, method=None, headers=None):
    raw = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(base + path, data=raw, method=method,
                                    headers={'Content-Type': 'application/json', **(headers or {})})
    try:
        response = urllib.request.urlopen(request, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        text = response.read().decode()
        return response.status, json.loads(text) if 'json' in response.headers.get('Content-Type', '') else text


def sql(command):
    return subprocess.run(['docker', 'exec', '-i', PG, 'psql', '-U', 'btsa', '-d', DB,
                           '-v', 'ON_ERROR_STOP=1', '-At'], input=command, text=True,
                          capture_output=True, check=True).stdout.strip()


def wait_for(predicate, timeout=100):
    until = time.monotonic() + timeout
    last = None
    while time.monotonic() < until:
        try:
            last = predicate()
            if last:
                return last
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(1)
    raise AssertionError(f'Timeout after {timeout}s; last={last}')


def get_transfer(identifier):
    code, value = http('/api/transfers/' + identifier)
    assert code == 200, (code, value)
    return value


def wait_status(identifier, status):
    return wait_for(lambda: (value := get_transfer(identifier))['status'] == status and value)


def publish(identifier):
    code, value = http('/api/exchanges/%2F/amq.default/publish', {
        'properties': {'delivery_mode': 2, 'content_type': 'application/json'},
        'routing_key': 'transfers.processing', 'payload': json.dumps({'TransferId': identifier}),
        'payload_encoding': 'string'}, base=BROKER, headers={'Authorization': AUTH})
    assert code == 200 and value['routed'], (code, value)


def reset():
    sql('''TRUNCATE audit_log, transfer_attempts, transfer_outbox_messages, transfers CASCADE;
        UPDATE accounts SET "Balance"=1000, "OverdraftLimit"=500, "Status"='active';
        UPDATE transfer_limit_policies SET "DayMaximumAmount"=10000, "NightMaximumAmount"=10000,
            "DayMaximumAttempts"=100, "NightMaximumAttempts"=100;''')


def future(seconds):
    return (dt.datetime.now(dt.timezone.utc) + dt.timedelta(seconds=seconds)).isoformat()


def send(amount=100, scheduled=None, key=None):
    request = {'sourceAccountId': SOURCE, 'method': 'Pix', 'pixKey': PIX, 'amount': amount}
    if scheduled:
        request['scheduledAt'] = scheduled
    return http('/api/transfers' + ('/scheduled' if scheduled else ''), request,
                headers={'Idempotency-Key': key or str(uuid.uuid4())})


def balance(identifier):
    return float(sql(f'SELECT "Balance" FROM accounts WHERE "Id"=\'{identifier}\';'))


def attempts():
    return int(sql('SELECT count(*) FROM transfer_attempts;'))


def metric():
    text = http('/metrics')[1]
    prefix = 'btsa_transfer_executions_total{outcome="completed"}'
    return next((float(line.split()[-1]) for line in text.splitlines() if line.startswith(prefix)), 0)


def smoke():
    assert http('/health/live')[0] == 200
    assert http('/health/ready')[0] == 200
    spec = http('/openapi/v1.json')[1]
    assert '/api/transfers' in spec['paths'] and '/api/transfers/scheduled' in spec['paths']
    assert http('/swagger/index.html')[0] == 200
    assert http('/', base=WEB)[0] == 200
    assert http('/swagger/index.html', base=WEB)[0] == 200
    assert '/api/transfers' in http('/openapi/v1.json', base=WEB)[1]['paths']
    assert len(http('/api/accounts', base=WEB)[1]['items']) == 13
    assert http('/api/accounts/' + SOURCE, base=WEB)[0] == 200


def immediate():
    reset()
    before = metric()
    key = str(uuid.uuid4())
    code, transfer = send(key=key)
    assert code == 200 and transfer['status'] == 'Completed', transfer
    assert send(key=key)[1]['id'] == transfer['id']
    assert balance(SOURCE) == 900 and balance(DESTINATION) == 1100 and attempts() == 1
    assert metric() == before + 1
    assert len(get_transfer(transfer['id'])['auditTrail']) == 2


def simultaneous():
    reset()
    sql(f'UPDATE accounts SET "Balance"=100, "OverdraftLimit"=0 WHERE "Id"=\'{SOURCE}\';')
    with concurrent.futures.ThreadPoolExecutor(2) as pool:
        values = list(pool.map(lambda _: send(80)[1], range(2)))
    assert sorted(value['status'] for value in values) == ['Completed', 'Failed'], values
    assert balance(SOURCE) == 20 and balance(DESTINATION) == 1080 and attempts() == 2


def scheduled_success():
    reset()
    run_at, key = future(3), str(uuid.uuid4())
    code, transfer = send(scheduled=run_at, key=key)
    assert code == 202 and transfer['status'] == 'Scheduled'
    wait_status(transfer['id'], 'Completed')
    assert send(scheduled=run_at, key=key)[1]['id'] == transfer['id']
    assert send(100.001, scheduled=run_at, key=key)[0] == 409
    publish(transfer['id'])
    time.sleep(3)
    assert balance(SOURCE) == 900 and balance(DESTINATION) == 1100 and attempts() == 1


def scheduled_insufficient():
    reset()
    transfer = send(scheduled=future(4))[1]
    sql(f'UPDATE accounts SET "Balance"=0, "OverdraftLimit"=0 WHERE "Id"=\'{SOURCE}\';')
    value = wait_status(transfer['id'], 'Failed')
    assert value['failureCode'] == 'account.insufficient_funds', value
    publish(transfer['id'])
    time.sleep(3)
    assert balance(SOURCE) == 0 and balance(DESTINATION) == 1000 and attempts() == 1
    assert sql('SELECT "ProcessingAttempts" FROM transfer_outbox_messages;') == '0'


def scheduled_limit_change():
    reset()
    transfer = send(scheduled=future(4))[1]
    sql(f'''UPDATE transfer_limit_policies SET "DayMaximumAmount"=50, "NightMaximumAmount"=50
        WHERE "AccountId"='{SOURCE}';''')
    value = wait_status(transfer['id'], 'Failed')
    assert value['failureCode'] == 'transfer.amount_limit_exceeded', value
    assert balance(SOURCE) == 1000 and balance(DESTINATION) == 1000 and attempts() == 1


def invalid_requests():
    reset()
    assert send(0)[0] == 400 and attempts() == 1
    assert send(100.001)[0] == 400 and attempts() == 2
    own_key = next(a for a in http('/api/accounts')[1]['items'] if a['id'] == SOURCE)['pixKeys'][0]['value']
    assert http('/api/transfers', {'sourceAccountId': SOURCE, 'method': 'Pix',
                                  'pixKey': own_key, 'amount': 10})[0] == 400
    assert attempts() == 3 and balance(SOURCE) == 1000


def bank_account():
    reset()
    destination = http('/api/accounts/' + DESTINATION)[1]
    request = {'sourceAccountId': SOURCE, 'method': 'BankAccount', 'amount': 0.29,
               'bankIspb': destination['ispb'], 'branch': destination['branch'],
               'accountNumber': destination['number'], 'checkDigit': destination['checkDigit']}
    code, transfer = http('/api/transfers', request)
    assert code == 200 and transfer['status'] == 'Completed', transfer
    assert balance(SOURCE) == 999.71 and balance(DESTINATION) == 1000.29


def cancellation():
    reset()
    transfer = send(scheduled=future(4))[1]
    assert http('/api/transfers/' + transfer['id'] + '/cancel', {})[1]['status'] == 'Cancelled'
    time.sleep(8)
    assert get_transfer(transfer['id'])['status'] == 'Cancelled'
    assert balance(SOURCE) == 1000 and attempts() == 0


def outbox_recovery():
    reset()
    transfer = send(scheduled=future(300))[1]
    sql('''UPDATE transfers SET "ScheduledAt"=now()-interval '5 seconds';
        UPDATE transfer_outbox_messages SET "AvailableAt"=now()-interval '5 seconds',
            "CreatedAt"=now()-interval '1 minute';''')
    wait_status(transfer['id'], 'Completed')
    assert balance(SOURCE) == 900 and attempts() == 1


def dead_letter():
    reset()
    transfer = send(scheduled=future(4))[1]
    identifier = transfer['id']
    sql(f'''CREATE OR REPLACE FUNCTION integration_fault() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN IF NEW."Id"='{identifier}' THEN RAISE EXCEPTION 'integration injected fault'; END IF;
        RETURN NEW; END; $$;
        CREATE TRIGGER integration_fault BEFORE UPDATE ON transfers FOR EACH ROW EXECUTE FUNCTION integration_fault();''')
    try:
        wait_for(lambda: get_transfer(identifier)['isInDeadLetter'])
        assert get_transfer(identifier)['status'] == 'Scheduled'
        assert balance(SOURCE) == 1000 and attempts() == 0
        assert int(sql('SELECT "ProcessingAttempts" FROM transfer_outbox_messages;')) == 6
        wait_for(lambda: any(json.loads(item['payload'])['TransferId'] == identifier for item in
                             http('/api/queues/%2F/transfers.processing.dead-letter/get',
                                  {'count': 100, 'ackmode': 'ack_requeue_true', 'encoding': 'auto'},
                                  base=BROKER, headers={'Authorization': AUTH})[1]))
    finally:
        sql('DROP TRIGGER IF EXISTS integration_fault ON transfers; DROP FUNCTION IF EXISTS integration_fault();')
    publish(identifier)
    wait_status(identifier, 'Completed')
    wait_for(lambda: not get_transfer(identifier)['processingError'])
    assert balance(SOURCE) == 900 and balance(DESTINATION) == 1100 and attempts() == 1


def commit_failure():
    reset()
    before = metric()
    sql('''CREATE OR REPLACE FUNCTION integration_commit_fault() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'integration commit failure'; END; $$;
        CREATE CONSTRAINT TRIGGER integration_commit_fault AFTER INSERT ON transfers
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION integration_commit_fault();''')
    try:
        code, problem = send()
        assert code == 500 and 'traceId' in problem, (code, problem)
        assert balance(SOURCE) == 1000 and balance(DESTINATION) == 1000
        assert sql('SELECT count(*) FROM transfers;') == '0' and attempts() == 0
        assert metric() == before
    finally:
        sql('DROP TRIGGER IF EXISTS integration_commit_fault ON transfers; DROP FUNCTION IF EXISTS integration_commit_fault();')


def broker_outage():
    reset()
    subprocess.run(COMPOSE + ['stop', 'rabbitmq'], check=True, capture_output=True)
    try:
        wait_for(lambda: http('/health/ready')[0] == 503)
        code, transfer = send(scheduled=future(3))
        assert code == 202, transfer
        time.sleep(5)
        assert get_transfer(transfer['id'])['status'] == 'Scheduled'
    finally:
        subprocess.run(COMPOSE + ['start', 'rabbitmq'], check=True, capture_output=True)
    wait_for(lambda: http('/health/ready')[0] == 200)
    wait_status(transfer['id'], 'Completed')
    assert balance(SOURCE) == 900 and attempts() == 1


def observability():
    grafana = 'http://127.0.0.1:23300'
    auth = {'Authorization': 'Basic ' + base64.b64encode(b'admin:btsa-local').decode()}
    assert http('/api/health', base=grafana)[1]['database'] == 'ok'
    targets = http('/api/v1/targets', base='http://127.0.0.1:29090')[1]['data']['activeTargets']
    assert targets and all(t['health'] == 'up' for t in targets), targets
    query = urllib.parse.urlencode({'query': '{app="teste-tecnico-btsa-api"}', 'limit': 1})
    logs = http('/api/datasources/proxy/uid/loki/loki/api/v1/query_range?' + query,
                base=grafana, headers=auth)
    assert logs[0] == 200 and logs[1]['data']['result'], logs


def production_routes():
    name = 'btsa-integration-production'
    production = 'http://127.0.0.1:25155'
    subprocess.run(COMPOSE + ['run', '-d', '--no-deps', '--name', name,
                              '-p', '127.0.0.1:25155:8080', '-e', 'ASPNETCORE_ENVIRONMENT=Production', 'api'],
                   check=True, capture_output=True)
    try:
        wait_for(lambda: http('/health/ready', base=production)[0] == 200)
        assert http('/swagger/index.html', base=production)[0] == 200
        assert http('/openapi/v1.json', base=production)[0] == 200
        accounts = http('/api/accounts', base=production)[1]['items']
        source = next(a for a in accounts if a['number'] == '10000001')
        destination = next(a for a in accounts if a['number'] == '10000002')
        assert http('/api/accounts/' + source['id'], base=production)[0] == 200
        assert http('/api/transfer-limit-policies', base=production)[0] == 404
        assert http('/api/accounts/' + source['id'] + '/status', {'status': 'Blocked'},
                    base=production, method='PUT')[0] == 404
        request = {'sourceAccountId': source['id'], 'method': 'Pix', 'amount': 0.07,
                   'pixKey': destination['pixKeys'][0]['value']}
        code, transfer = http('/api/transfers', request, base=production)
        assert code == 200 and transfer['status'] == 'Completed', transfer
        assert http('/api/transfers/' + transfer['id'], base=production)[0] == 200
        code, transfer = http('/api/transfers/scheduled', {**request, 'scheduledAt': future(300)}, base=production)
        assert code == 202, transfer
        assert http('/api/transfers/' + transfer['id'] + '/cancel', {}, base=production)[1]['status'] == 'Cancelled'
    finally:
        subprocess.run(['docker', 'rm', '-f', name], check=True, capture_output=True)


if __name__ == '__main__':
    project = subprocess.check_output(['docker', 'inspect', PG, '--format',
                                      '{{index .Config.Labels "com.docker.compose.project"}}'], text=True).strip()
    assert project == 'btsa-integration' and DB.endswith('_test'), 'Refusing non-test environment'
    wait_for(lambda: http('/health/ready')[0] == 200)
    accounts = http('/api/accounts')[1]['items']
    origin = next(a for a in accounts if a['number'] == '10000001')
    destination = next(a for a in accounts if a['number'] == '10000002')
    SOURCE, DESTINATION, PIX = origin['id'], destination['id'], destination['pixKeys'][0]['value']
    for scenario in [smoke, immediate, bank_account, invalid_requests, simultaneous, scheduled_success, scheduled_insufficient, scheduled_limit_change,
                     cancellation, outbox_recovery, dead_letter, commit_failure, broker_outage,
                     observability, production_routes]:
        print(f'RUN {scenario.__name__}', flush=True)
        scenario()
        print(f'PASS {scenario.__name__}', flush=True)
