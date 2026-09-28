using TesteTecnico.Api.Features.Accounts;
using TesteTecnico.Api.Features.TransferLimits;
using TesteTecnico.Api.Features.Transfers;
using TesteTecnico.Api.Features.Accounts.GetAccounts;
using TesteTecnico.Api.Features.Accounts.GetAccount;

namespace TesteTecnico.Api.Infrastructure.Endpoints;

/// <summary>Compõe o grupo HTTP versionado da aplicação.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>Mapeia os endpoints da API sob a rota base /api.</summary>
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.MapGetAccountsEndpoints();
        api.MapGetAccountByIdEndpoints();
        api.MapTransferEndpoints();
        return endpoints;
    }

    /// <summary>Mapeia rotas locais de configuração usadas para preparar cenários de demonstração.</summary>
    public static IEndpointRouteBuilder MapDevelopmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.MapDevelopmentAccountEndpoints();
        api.MapTransferLimitPolicyEndpoints();
        return endpoints;
    }
}
