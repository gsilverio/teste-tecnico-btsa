using TesteTecnico.Api.Features.Accounts.GetAccounts;
using TesteTecnico.Api.Features.Accounts.GetAccount;
using TesteTecnico.Api.Features.Accounts.ManageOverdraft;
using TesteTecnico.Api.Features.Accounts.ManageStatus;

namespace TesteTecnico.Api.Features.Accounts;

/// <summary>Registro das rotas dos casos de uso de conta.</summary>
public static class AccountsEndpointRegistration
{
    /// <summary>Delega o registro HTTP aos slices de consulta e cheque especial.</summary>
    public static RouteGroupBuilder MapAccountsEndpoints(this RouteGroupBuilder group) =>
        group.MapDevelopmentAccountEndpoints();

    /// <summary>Registra alterações de estado e cheque especial para o ambiente de demonstração.</summary>
    public static RouteGroupBuilder MapDevelopmentAccountEndpoints(this RouteGroupBuilder group) =>
        group.MapManageAccountStatusEndpoints()
            .MapManageOverdraftEndpoints();
}
