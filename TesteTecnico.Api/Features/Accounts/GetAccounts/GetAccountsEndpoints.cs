using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Accounts.GetAccounts;

/// <summary>Rotas de consulta de contas.</summary>
public static class GetAccountsEndpoints
{
    /// <summary>Registra a rota paginada de consulta de contas.</summary>
    public static RouteGroupBuilder MapGetAccountsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/accounts", async (
                int? page,
                int? pageSize,
                GetAccountsQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var pagination = PaginationRequest.FromQuery(page, pageSize);
                if (pagination.ToHttpResultIfInvalid(httpContext, "account.invalid_pagination") is { } invalidPagination)
                {
                    return invalidPagination;
                }

                var result = await handler.HandleAsync(pagination, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetAccounts")
            .WithTags("Accounts")
            .WithSummary("Lista contas cadastradas")
            .WithDescription("Retorna contas paginadas com titular, banco, saldo, cheque especial e chaves Pix para facilitar os testes.")
            .Produces<Page<AccountSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}
