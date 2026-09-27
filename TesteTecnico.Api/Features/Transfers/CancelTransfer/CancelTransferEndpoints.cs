using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.Transfers.CancelTransfer;

/// <summary>Registra o cancelamento de uma transferência agendada.</summary>
public static class CancelTransferEndpoints
{
    /// <summary>Registra a rota de cancelamento por identificador.</summary>
    public static RouteGroupBuilder MapCancelTransferEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/transfers/{transferId:guid}/cancel", async (
                Guid transferId,
                CancelTransferCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(transferId, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("CancelTransfer")
            .WithTags("Transfers")
            .WithSummary("Cancela uma transferência agendada")
            .WithDescription("O estado Scheduled é alterado atomicamente para Cancelled; execução e cancelamento concorrentes têm um único vencedor.")
            .Audit(AuditActionType.CancelTransfer)
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
