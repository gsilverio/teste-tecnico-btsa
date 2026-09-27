using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Transfers.GetTransfer;

/// <summary>Registra a consulta de uma transferência.</summary>
public static class GetTransferEndpoints
{
    /// <summary>Registra a rota GET de transferência por identificador.</summary>
    public static RouteGroupBuilder MapGetTransferEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transfers/{transferId:guid}", async (
                Guid transferId,
                GetTransferQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(transferId, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetTransfer")
            .WithTags("Transfers")
            .WithSummary("Consulta uma transferência")
            .WithDescription("Retorna o estado e os dados persistidos da transferência, incluindo falhas de negócio e cancelamento.")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
