# teste-tecnico-btsa

Aplicação de transferências bancárias via Pix ou agência/conta, com execução imediata, agendamento, cancelamento e consulta de detalhes. Inclui um painel React para consultar saldos e configurar cheque especial, limites e situação das contas.

## Executar a aplicação completa

Pré-requisitos: Docker Desktop com containers Linux (ou Docker Engine com Compose), acesso à internet no primeiro build e as portas abaixo disponíveis. Não é necessário instalar .NET ou Node na máquina para esta modalidade.

Depois de clonar o repositório, execute na raiz:

```powershell
docker compose --env-file .env.dev up -d --build
```

Aguarde a inicialização do banco, do RabbitMQ e da API. As migrations e o seed são executados automaticamente. Os arquivos `.env.dev` da raiz e do frontend estão versionados com configurações de demonstração; não é necessário criá-los manualmente.

| Serviço             | Endereço                           | Credenciais locais     |
| ------------------- | ---------------------------------- | ---------------------- |
| Frontend            | http://localhost:3001              | Sem autenticação       |
| Swagger             | http://localhost:5154/swagger      | Sem autenticação       |
| Hangfire            | http://localhost:5154/hangfire     | Ambiente Development   |
| RabbitMQ Management | http://localhost:15673             | `btsa` / `btsa-local`  |
| Grafana             | http://localhost:3300              | `admin` / `btsa-local` |
| Prometheus          | http://localhost:19090             | Sem autenticação       |
| PostgreSQL          | `localhost:5434`, banco `postgres` | `btsa` / `btsa-local`  |

A API é compilada em **Release**, mas o Compose usa **Development** como ambiente de execução para habilitar o seed e as configurações administrativas usadas na avaliação. Trata-se do ambiente de demonstração do teste.

Para verificar a inicialização e parar os serviços preservando os dados:

```powershell
docker compose --env-file .env.dev ps
docker compose --env-file .env.dev logs --tail 100 api
docker compose --env-file .env.dev down
```

## Executar em desenvolvimento

Além do Docker, instale .NET SDK 10 e Node.js 24 com npm. Na raiz, suba somente as dependências:

```powershell
docker compose --env-file .env.dev up -d postgres rabbitmq
dotnet run --project TesteTecnico.Api --launch-profile http
```

Em outro terminal, inicie o frontend:

```powershell
cd tt-btsa-webapp
npm ci
npm run dev
```

Abra http://localhost:3001. A API local usa `appsettings.Development.json` para conectar ao PostgreSQL em `localhost:5434` e ao RabbitMQ em `localhost:5673`. O frontend carrega `tt-btsa-webapp/.env.dev`: `VITE_API_BASE_URL` vazio utiliza `/api`, e `VITE_API_PROXY_TARGET` aponta o proxy do Vite para a API em `http://127.0.0.1:5154`.

Se a aplicação completa já estiver rodando no Compose, execute `docker compose stop api frontend` antes de iniciar esses processos localmente, liberando as portas. Alterações nas variáveis do frontend exigem reiniciar o Vite.

## Configurações disponíveis e escopo

Pelo painel ou pelo Swagger, no ambiente de demonstração, é possível configurar individualmente por conta:

- **Cheque especial:** ajustar o valor disponível para uso além do saldo.
- **Status da conta:** ativar, bloquear ou inativar a conta para testar as restrições de envio e recebimento.
- **Limites de transferência:** configurar os tetos monetários e a quantidade de tentativas por hora, separados entre dia e noite.

Os saldos iniciais são definidos diretamente pelo seed. Não há edição manual de saldo no painel nem endpoint para alterá-lo; após a inicialização, os saldos mudam conforme as transferências concluídas. O seed também fornece bancos, titulares e contas para os testes.

O foco deste projeto é a execução das transações e suas regras de negócio. Por isso, endpoints complementares, como cadastro completo de contas ou ajuste manual de saldo, ficaram fora do escopo. As configurações disponíveis permitem reproduzir diferentes cenários de validação das transferências.

## Decisões arquiteturais

- **Vertical Slice Architecture e DDD:** `Features` organiza endpoints, contratos e handlers por caso de uso; `Domain` concentra entidades e regras; `Infrastructure` reúne persistência, mensageria e serviços técnicos. Minimal APIs registram as rotas por funcionalidade, com dependências resolvidas por DI. O Result Pattern representa falhas esperadas de negócio, e DTOs separam os contratos HTTP das entidades.
- **Persistência e concorrência:** EF Core e PostgreSQL persistem valores monetários em `numeric(18,2)` e enums nativos. Migrations versionam o schema. Débito, crédito, estado e auditoria de domínio são confirmados na mesma transação; locks de linha nas contas, adquiridos em ordem determinística, evitam que transferências simultâneas gastem o mesmo saldo.
- **Transferência imediata e agendada:** a imediata aguarda o resultado no HTTP. A agendada usa Hangfire para disparar a publicação no RabbitMQ e um consumidor para processar a fila. Ambos reutilizam o mesmo executor financeiro. Hangfire e consumidor rodam dentro da API, simplificando a execução do teste.
- **Entrega e recuperação:** o agendamento e sua outbox são persistidos juntos. Uma rotina recupera publicações pendentes; o consumidor confirma a mensagem após o commit. Idempotência e verificação de estados terminais impedem movimentação duplicada. Rejeições de negócio encerram a transferência como `Failed`; falhas técnicas têm retries limitados e dead-letter queue.
- **Uma conta por titular:** cada conta pertence a um titular, que pode possuir no máximo uma conta, com unicidade garantida no banco. Cheque especial e política de transferência continuam associados à conta; como cada titular possui uma única conta, seus limites também são individuais por pessoa. Limites e status são reavaliados ao executar um agendamento.
- **Eventos e auditoria:** `TransferStateChangedDomainEvent` representa fatos ocorridos no domínio. Esses eventos originam registros em `audit_log` dentro da transação financeira; auditoria HTTP registra a ação e seu resultado. Isso não constitui event sourcing: o estado atual continua persistido nas entidades.
- **Observabilidade:** Serilog produz logs estruturados com contexto do componente; Loki armazena os logs, Prometheus coleta métricas e Grafana permite consultá-los. Health checks distinguem processo ativo de dependências disponíveis.
- **Frontend:** React e TypeScript separam `pages`, `components`, `hooks`, `services` e `types`. Axios centraliza o acesso HTTP. Vite em desenvolvimento e Nginx no Compose encaminham as chamadas para a API pelo mesmo caminho `/api`.

## Diagrama de classes

Visão resumida das classes existentes, com seus principais atributos e relacionamentos. As associações por identificador não implicam propriedades de navegação C#. `TransferOutboxMessage` pertence à infraestrutura; as setas tracejadas representam produção ou conversão de eventos, não chaves estrangeiras.

```mermaid
classDiagram
    class AccountHolder {
        Guid Id
        string Name
        string Cpf
    }
    class Bank {
        Guid Id
        string Name
        string Ispb
        string CompeCode
    }
    class Account {
        Guid Id
        Guid OwnerId
        Guid BankId
        BankAccountType Type
        string Branch
        string Number
        decimal Balance
        decimal OverdraftLimit
        AccountStatus Status
        ValidateDebit()
        ValidateCredit()
        Block()
        Activate()
    }
    class PixKey {
        PixKeyType Type
        string Value
    }
    class TransferLimitPolicy {
        Guid Id
        Guid AccountId
        decimal DayMaximumAmount
        int DayMaximumAttempts
        decimal NightMaximumAmount
        int NightMaximumAttempts
        Update()
    }
    class Transfer {
        Guid Id
        Guid SourceAccountId
        Guid DestinationAccountId
        decimal Amount
        TransferMethod Method
        TransferStatus Status
        DateTimeOffset ScheduledAt
        DateTimeOffset FinishedAt
        DateTimeOffset CancelledAt
        string IdempotencyKey
        string FailureCode
        Complete()
        Fail()
        Cancel()
    }
    class TransferAttempt {
        Guid Id
        Guid TransferId
        Guid SourceAccountId
        DateTimeOffset AttemptedAt
        string FailureCode
        string IdempotencyKey
    }
    class TransferOutboxMessage {
        Guid Id
        Guid TransferId
        DateTimeOffset AvailableAt
        DateTimeOffset PublishedAt
        int ProcessingAttempts
        DateTimeOffset DeadLetteredAt
    }
    class TransferStateChangedDomainEvent {
        Guid EventId
        Guid TransferId
        TransferStatus PreviousStatus
        TransferStatus Status
        int Sequence
        DateTimeOffset OccurredAt
    }
    class AuditEntry {
        Guid Id
        AuditSource Source
        AuditActionType ActionType
        Guid TransferId
        DateTimeOffset OccurredAt
        FromDomainEvent()
    }

    AccountHolder "1" --> "0..1" Account : titular
    Bank "1" --> "0..*" Account : banco
    Account "1" *-- "0..*" PixKey : chaves
    Account "1" --> "0..1" TransferLimitPolicy : politica
    Account "1" --> "0..*" Transfer : origem
    Account "1" --> "0..*" Transfer : destino
    Account "1" --> "0..*" TransferAttempt : tentativas
    Transfer "0..1" --> "0..1" TransferAttempt : execucao
    Transfer "1" --> "0..1" TransferOutboxMessage : despacho agendado
    Transfer ..> TransferStateChangedDomainEvent : emite
    AuditEntry ..> TransferStateChangedDomainEvent : criado a partir de
```
