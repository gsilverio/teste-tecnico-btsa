using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.CancelTransfer;

/// <summary>Aplica atomicamente o cancelamento de um agendamento.</summary>
public sealed class CancelTransferCommandHandler(AppDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>Cancela somente transferências que ainda estão agendadas.</summary>
    public async Task<Result<TransferResponse>> HandleAsync(Guid transferId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var transfer = await dbContext.Transfers
            .FromSqlInterpolated($"SELECT * FROM \"transfers\" WHERE \"Id\" = {transferId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (transfer is null)
        {
            return Result<TransferResponse>.Failure(new Error("transfer.not_found", "A transferência não foi encontrada."));
        }

        var cancellation = transfer.Cancel(timeProvider.GetUtcNow());
        if (!cancellation.IsSuccess)
        {
            return Result<TransferResponse>.Failure(cancellation.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
            transfer.FailureCode));
    }
}
