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
        group.MapGetAccountsEndpoints()
            .MapGetAccountByIdEndpoints()
            .MapManageAccountStatusEndpoints()
            .MapManageOverdraftEndpoints();
}
