# BTSA · Frontend

Aplicação React + TypeScript para consultar contas, administrar limites e operar transferências de demonstração.

## Executar localmente

1. Suba PostgreSQL e RabbitMQ pelo Compose na raiz do repositório e inicie a API em `http://localhost:5154`.
2. Nesta pasta, instale as dependências e inicie o Vite:

```powershell
npm install
npm run dev
```

Abra `http://localhost:3001`. `npm run dev` carrega `.env.dev`: `VITE_APP_BASE_URL` define o caminho-base do frontend e `VITE_API_PROXY_TARGET` indica para onde o Vite encaminha as chamadas `/api/*` durante o desenvolvimento. `VITE_API_BASE_URL` é o caminho-base completo da API e por padrão vale `/api`; assim, a chamada de contas sempre fica em `/api/accounts`. Para outra origem, configure o endereço completo incluindo o prefixo, por exemplo `https://api.exemplo.com/api`.

## Funcionalidades

O link do Swagger usa a mesma origem configurada para a API. Vite e Nginx encaminham `/api`, `/swagger` e `/openapi`, inclusive quando o ambiente usa portas diferentes das de desenvolvimento.

- Carrega `GET /api/accounts` e `GET /api/transfer-limit-policies`.
- Cria políticas com `POST /api/transfer-limit-policies`, atualiza com `PUT /api/transfer-limit-policies/{policyId}` e remove com `DELETE /api/transfer-limit-policies/{policyId}`.
- Mostra titular, banco, agência/conta, saldo, cheque especial e estado da política. A política é associada à conta por `accountId`.
- Permite atualizar ou zerar o cheque especial com `PUT` e `DELETE /api/accounts/{accountId}/overdraft-limit`.
- As contas e as políticas são paginadas pela API; a tela solicita até 100 itens por coleção, suficiente para o seed local.
- A tela de transferências solicita operações Pix ou por agência/conta. A operação imediata espera o processamento síncrono e mostra o resultado final; o agendamento retorna assim que fica registrado para processamento futuro. Ambos enviam uma chave de idempotência.
- O acompanhamento consulta `GET /api/transfers/{id}` e permite cancelar enquanto a transferência estiver agendada. Como a API ainda não oferece listagem de transferências, os IDs recentes ficam salvos neste navegador.
- As transferências agendadas são revalidadas pelo backend no processamento; rejeições mostram o código/motivo e não alteram parcialmente os saldos.

## Arquitetura

`pages/` compõe as telas; `components/` mantém os elementos visuais; `hooks/` coordena estado de consulta e comandos; `services/` centraliza uma instância Axios e as rotas por domínio; `types/` define os contratos da API. `App` apenas monta a página principal. As diretivas de arquitetura específicas do frontend estão em `../dotnet-btsa-frontend.mdc`.

## Verificações

```powershell
npm run build
npm run lint
npm test
```
