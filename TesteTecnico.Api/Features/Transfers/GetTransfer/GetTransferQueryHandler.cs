using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.GetTransfer;

/// <summary>Consulta o estado persistido de uma transferência.</summary>
public sealed class GetTransferQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna os dados públicos da transferência solicitada.</summary>
    public async Task<Result<TransferResponse>> HandleAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var transfer = await dbContext.Transfers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == transferId, cancellationToken);

        if (transfer is null)
        {
            return Result<TransferResponse>.Failure(new Error("transfer.not_found", "A transferência não foi encontrada."));
        }

        var auditEntries = await dbContext.AuditEntries.AsNoTracking()
            .Where(entry => entry.Source == AuditSource.DomainEvent && entry.TransferId == transferId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new
            {
                entry.Id,
                entry.ActionType,
                entry.Sequence,
                entry.PreviousStatus,
                entry.Status,
                entry.OccurredAt,
                entry.ErrorMessage
            })
            .ToArrayAsync(cancellationToken);
        var auditTrail = auditEntries.Select(entry => new TransferAuditEventResponse(
            entry.Id,
            TransferActionName(entry.ActionType),
            entry.Sequence!.Value,
            entry.PreviousStatus?.ToString(),
            entry.Status!.Value.ToString(),
            entry.OccurredAt,
            entry.ErrorMessage)).ToArray();
        var dispatchState = await dbContext.TransferOutboxMessages.AsNoTracking()
            .Where(message => message.TransferId == transferId)
            .Select(message => new { message.LastProcessingError, message.DeadLetteredAt })
            .SingleOrDefaultAsync(cancellationToken);

        return Result<TransferResponse>.Success(new TransferResponse(
            transfer.Id,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.Amount,
            transfer.Method.ToString(),
            transfer.Status.ToString(),
            transfer.CreatedAt,
            transfer.ScheduledAt,
            transfer.FinishedAt,
            transfer.CancelledAt,
            transfer.FailureCode,
            auditTrail,
            dispatchState?.LastProcessingError,
            dispatchState?.DeadLetteredAt is not null));
    }

    private static string TransferActionName(AuditActionType actionType) => actionType switch
    {
        AuditActionType.TransferRequested => "Requested",
        AuditActionType.TransferScheduled => "Scheduled",
        AuditActionType.TransferProcessingStarted => "ProcessingStarted",
        AuditActionType.TransferCompleted => "Completed",
        AuditActionType.TransferFailed => "Failed",
        AuditActionType.TransferCancelled => "Cancelled",
        _ => actionType.ToString()
    };
}
