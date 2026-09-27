using TesteTecnico.Api.Features.Accounts.GetAccounts;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Accounts.GetAccount;

/// <summary>Rotas para consulta individual de conta.</summary>
public static class GetAccountByIdEndpoints
{
    /// <summary>Registra a rota GET de conta por identificador.</summary>
    public static RouteGroupBuilder MapGetAccountByIdEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/accounts/{accountId:guid}", async (
                Guid accountId,
                GetAccountByIdQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(accountId, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetAccountById")
            .WithTags("Accounts")
            .WithSummary("Consulta uma conta por identificador")
            .Audit(AuditActionType.ViewAccountDetails)
            .Produces<AccountSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
