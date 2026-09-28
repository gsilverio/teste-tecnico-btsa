using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Transfers.ListAccountTransfers;

/// <summary>Registra a consulta paginada das transferências de uma conta.</summary>
public static class ListAccountTransfersEndpoints
{
    public static RouteGroupBuilder MapListAccountTransfersEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/accounts/{accountId:guid}/transfers", async (
                Guid accountId,
                int? page,
                int? pageSize,
                ListAccountTransfersQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var pagination = PaginationRequest.FromQuery(page, pageSize);
                if (pagination.ToHttpResultIfInvalid(httpContext, "transfer.invalid_pagination") is { } invalidPagination)
                {
                    return invalidPagination;
                }

                var result = await handler.HandleAsync(accountId, pagination, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("ListAccountTransfers")
            .WithTags("Transfers")
            .WithSummary("Lista transferências de uma conta")
            .WithDescription("Retorna envios e recebimentos da conta, dos mais recentes aos mais antigos.")
            .Produces<Page<AccountTransferSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
