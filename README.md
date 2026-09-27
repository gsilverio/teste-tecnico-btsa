# teste-tecnico-btsa

## API

A API local usa PostgreSQL e RabbitMQ do Compose em `localhost:5434` e `localhost:5673`. Para subir o ambiente completo em containers, incluindo a API publicada em Release, frontend, Grafana, Loki e Prometheus, basta executar na raiz:

```powershell
docker compose up
```

O Compose carrega as configurações internas do arquivo `.env.dev`. O seed de demonstração é aplicado pela API quando ela inicia. Para usar os containers de infraestrutura e executar a API/frontend pelo Visual Studio e Vite, use `docker compose up -d postgres rabbitmq` e siga as instruções de execução local abaixo.

As migrations são aplicadas automaticamente na inicialização da API. Hangfire roda dentro do processo da API e usa o PostgreSQL; a API também abre a conexão com RabbitMQ no startup. A aplicação para se não conseguir conectar ao banco, atualizar o schema ou abrir o broker. A imagem da API publica o projeto em `Release`; o container usa o ambiente `Development` para disponibilizar Swagger, dashboard Hangfire, endpoints de configuração e os dados de demonstração.

Em Development, depois das migrations, um seed idempotente garante três bancos fictícios, dez titulares com CPFs sintéticos e treze contas com saldos, chaves Pix e políticas de demonstração. O primeiro titular possui contas em bancos diferentes; há contas bloqueada e inativa para testar envio e recebimento. O seed não substitui configurações já alteradas; assim, mudanças feitas durante os testes permanecem após reiniciar a API.

Os CPFs de demonstração são sintéticos; a migration preenche os titulares antigos com esses identificadores porque o cadastro anterior não guardava CPF. As respostas da API mostram o CPF mascarado.

Em Development, use `GET /api/accounts` e `GET /api/accounts/{accountId}` para consultar IDs, titular/CPF mascarado, banco, saldo, status, cheque especial e chaves Pix. As contas para teste vêm do seed; não há fluxo de cadastro de conta porque esse caso de uso não faz parte do foco do teste.

`PUT /api/accounts/{accountId}/status` recebe `{"status":"Blocked"}`, `{"status":"Inactive"}` ou `{"status":"Active"}` para bloquear, inativar ou reativar a conta. A mudança usa lock de linha e é serializada com a transferência em execução. Contas bloqueadas ou inativas falham como origem e como destino, e transferências agendadas reavaliam o status na hora do processamento. Os endpoints de cheque especial são `GET` e `PUT /api/accounts/{accountId}/overdraft-limit`; `DELETE` define o limite como zero e falha se a conta já estiver usando o cheque especial.

As políticas de limite pertencem à conta, assim como o cheque especial. Cada conta pode ter uma política de transferência; titular/CPF e banco são contexto cadastral, não definem o escopo do limite. Em Development, o CRUD está disponível em `GET/POST /api/transfer-limit-policies`, `GET/PUT/DELETE /api/transfer-limit-policies/{policyId}`. A criação recebe `accountId`; a política configura valores máximos e quantidade de tentativas de dia e de noite, persistidos para edição nos testes. Essas rotas de consulta/configuração ficam desabilitadas fora de Development.

O fluxo de transferências usa `POST /api/transfers` para transferências imediatas síncronas, `POST /api/transfers/scheduled` para agendamentos, `GET /api/transfers/{id}` para acompanhar o estado e `POST /api/transfers/{id}/cancel` para cancelar somente enquanto o estado for `Scheduled`. A transferência imediata aguarda o mesmo executor financeiro usado pelo consumidor RabbitMQ e retorna `200` com o resultado final (`Completed` ou `Failed`). O agendamento retorna `202` e é processado por Hangfire e RabbitMQ na data informada. Os dois POST aceitam o header opcional `Idempotency-Key`. Repetir a chave com os mesmos dados retorna a transferência existente; reutilizá-la com conteúdo diferente gera conflito. A modalidade é `Pix`, com `pixKey`, ou `BankAccount`, com `bankIspb`, `branch`, `accountNumber` e `checkDigit` opcional. Por exemplo:

```json
{
  "sourceAccountId": "019942d0-0010-7000-8000-000000000010",
  "method": "Pix",
  "amount": 100.00,
  "pixKey": "conta10000002@example.com"
}
```

O endpoint agendado recebe os mesmos dados e um `scheduledAt` futuro em ISO 8601 com offset. Ele retorna `202 Accepted`; consulte o recurso para acompanhar `Scheduled`, `Processing`, `Completed`, `Failed` ou `Cancelled`. A transferência imediata retorna o estado final na própria resposta; uma falha de regra de negócio inclui `failureCode`, sem débito ou crédito parcial.

Transferências imediatas persistem e executam dentro da mesma transação PostgreSQL, então a resposta só chega depois do resultado financeiro. Agendamentos persistem com uma mensagem de outbox na mesma transação; Hangfire publica no horário escolhido. O consumidor RabbitMQ roda dentro do mesmo processo da API e usa o mesmo executor financeiro da chamada síncrona. A fila durável é `transfers.processing`; o consumidor usa ack manual e só confirma após o commit do débito, crédito, tentativa e estado. A rotina de recuperação republica itens vencidos da outbox se o processo cair antes do despacho Hangfire ou entre o commit da publicação e sua marcação local. Mensagens repetidas são seguras: estados terminais não movimentam saldo novamente. Mensagens inválidas ou com falhas técnicas após cinco retries vão para `transfers.processing.dead-letter`.

Na execução, a API bloqueia origem e destino em ordem determinística e bloqueia a política da conta de origem. Ela verifica situação ativa de ambas as contas, saldo com cheque especial, limite monetário e tentativas numa janela móvel de 60 minutos. Ambos os fluxos registram a tentativa no momento da execução. Rejeições financeiras também ficam como `Failed`, sem movimentação parcial. Agendamentos reavaliam todas as condições no momento da execução. Dia é de 06:00 a 22:00 e noite de 22:00 a 06:00 no fuso `America/Sao_Paulo`, conforme a configuração `TransferRules`.

As rotas anotadas persistem a ação HTTP, o modelo da rota, o resultado e identificadores de rota sem registrar corpos de requisição. As mudanças do domínio de transferência também emitem eventos que entram em `audit_log` junto com as alterações financeiras na mesma transação. `GET /api/transfers/{id}` devolve esse histórico; a interface o mostra nos detalhes da transferência. Registros de API e de domínio são distinguidos por `source`.

Os testes de integração do processamento usam PostgreSQL real. Configure `BTSA_TEST_CONNECTION` para um banco descartável cujo nome termine em `_test` antes de executar `dotnet test TesteTecnico.Api.Tests/TesteTecnico.Api.Tests.csproj`; a suíte limpa as tabelas de negócio e auditoria entre os casos. Não aponte essa variável para o banco local de desenvolvimento.

Para criar uma migration, na pasta `TesteTecnico.Api`:

```powershell
dotnet tool restore
dotnet ef migrations add <NomeDaMigration> --context AppDbContext --output-dir Infrastructure/Migrations
```

Para subir apenas PostgreSQL e RabbitMQ durante o desenvolvimento local, na raiz do repositório:

```powershell
docker compose --file docker-compose.yaml --env-file .env.dev up --detach
```

Depois inicie a API localmente, na pasta `TesteTecnico.Api`. O perfil de inicialização abre automaticamente a interface Swagger:

```powershell
dotnet run --launch-profile http
```

Endereços locais: frontend `http://localhost:3001`, API `http://localhost:5154`, Swagger `http://localhost:5154/swagger`, documento OpenAPI `http://localhost:5154/openapi/v1.json`, Hangfire `http://localhost:5154/hangfire`, RabbitMQ Management `http://localhost:15673`, Grafana `http://localhost:3300` e Prometheus `http://localhost:19090`. O usuário/senha local do Grafana são `admin` / `btsa-local`.

As credenciais de demonstração ficam em `.env.dev`; o Compose as injeta nos serviços. A configuração `appsettings.Development.json` mantém os endereços `localhost` para executar a API localmente. A instância Postgres do Compose publica a porta `5434`, deixando as portas `5432` e `5433` livres para instâncias Postgres já instaladas na máquina. Essas credenciais são apenas para execução local, não para produção.

## Frontend

O painel React/TypeScript em `tt-btsa-webapp` consulta contas e políticas, permite ver detalhes das contas, bloquear/inativar/reativar contas, configurar limites de transferência e editar o cheque especial. Na tela de transferências, contas não ativas continuam selecionáveis para reproduzir a rejeição e exibir o motivo. Na execução local, com a API rodando, inicie-o em outra janela:

```powershell
cd tt-btsa-webapp
npm install
npm run dev
```

Abra `http://localhost:3001`. O Vite encaminha as chamadas `/api` para `http://localhost:5154`; o Swagger continua em `http://localhost:5154/swagger`. Os contratos e fluxos ficam organizados em `src/components`, `src/pages`, `src/services`, `src/hooks` e `src/types`; as diretivas de frontend estão em `dotnet-btsa-frontend.mdc`. A página de transferência permite abrir os detalhes completos pelo ID, com atualização do status enquanto o agendamento estiver pendente.

## Logging

A API usa Serilog no console; no Compose, envia os eventos também ao Loki pelo sink Serilog. Cada evento inclui timestamp, nível e `SourceContext` para identificar a classe ou componente que o emitiu. Inicialização, migrations, conexões com RabbitMQ e requisições HTTP bem-sucedidas são registrados em `Information`; detalhes de rotina ficam em `Debug` (habilitado em Development). Requisições 4xx são `Warning`, falhas 5xx e exceções são `Error` ou `Fatal`.

O Grafana provisiona automaticamente as fontes Prometheus e Loki e o dashboard `BTSA / BTSA API - Logs`. Ele consulta os logs da API com o rótulo `{app="teste-tecnico-btsa-api"}`; os arquivos ficam persistidos no volume `loki-data` com retenção de sete dias. A API continua escrevendo no console, mesmo quando Loki não está configurado para uma execução local.

Para parar os serviços, rode `docker compose down`. Para apagar também os dados persistidos nos volumes do Compose, use `docker compose down --volumes`.
