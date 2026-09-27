using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.ExecuteTransfer;

/// <summary>Executa atomicamente uma transferência recebida pela fila RabbitMQ.</summary>
public sealed class ExecuteTransferCommandHandler(
    AppDbContext dbContext,
    TransferLimitEvaluator limitEvaluator,
    TimeProvider timeProvider,
    ILogger<ExecuteTransferCommandHandler> logger)
{
    /// <summary>Aplica saldo, limites e estado terminal dentro de uma única transação PostgreSQL.</summary>
    public async Task HandleAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.Transfers.AsNoTracking()
            .SingleOrDefaultAsync(transfer => transfer.Id == transferId, cancellationToken);
        if (snapshot is null)
        {
            logger.LogWarning("RabbitMQ delivered unknown transfer {TransferId}; the message will be acknowledged", transferId);
            return;
        }

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        // Lock both accounts in a stable order so opposite-direction transfers cannot deadlock.
        var accounts = await dbContext.Accounts.FromSqlInterpolated($"""
                SELECT * FROM "accounts"
                WHERE "Id" = {snapshot.SourceAccountId} OR "Id" = {snapshot.DestinationAccountId}
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        var transfer = await dbContext.Transfers.FromSqlInterpolated($"SELECT * FROM \"transfers\" WHERE \"Id\" = {transferId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (transfer is null || transfer.Status is TransferStatus.Completed or TransferStatus.Failed or TransferStatus.Cancelled)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogDebug("Transfer {TransferId} was already terminal when its queue message was processed", transferId);
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (transfer.Status == TransferStatus.Scheduled)
        {
            if (transfer.ScheduledAt is null || transfer.ScheduledAt > now)
            {
                throw new TransferNotReadyException(transfer.Id, transfer.ScheduledAt);
            }

            var begin = transfer.BeginScheduledProcessing(now);
            if (!begin.IsSuccess)
            {
                throw new InvalidOperationException($"A transferência {transfer.Id} não pôde iniciar: {begin.Error!.Code}.");
            }
        }
        else if (transfer.Status != TransferStatus.Processing)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return;
        }

        var attemptExists = await dbContext.TransferAttempts.AnyAsync(
            attempt => attempt.TransferId == transfer.Id,
            cancellationToken);
        if (!attemptExists)
        {
            dbContext.TransferAttempts.Add(TransferAttempt.Create(transfer.Id, transfer.SourceAccountId, now));
        }

        var source = accounts.SingleOrDefault(account => account.Id == transfer.SourceAccountId);
        var destination = accounts.SingleOrDefault(account => account.Id == transfer.DestinationAccountId);
        var rejectionCode = source is null || destination is null
            ? "transfer.account_missing"
            : await EvaluateBusinessRulesAsync(source, destination, transfer, now, cancellationToken);

        if (rejectionCode is not null)
        {
            FailTransfer(transfer, rejectionCode, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation(
                "Transfer {TransferId} was rejected with code {FailureCode}; the attempt was recorded",
                transfer.Id,
                rejectionCode);
            return;
        }

        var debit = source!.Debit(transfer.Amount);
        var credit = destination!.Credit(transfer.Amount);
        if (!debit.IsSuccess || !credit.IsSuccess)
        {
            throw new InvalidOperationException("A validação financeira mudou durante uma execução que mantém os locks das contas.");
        }

        var completion = transfer.Complete(now);
        if (!completion.IsSuccess)
        {
            throw new InvalidOperationException($"A transferência {transfer.Id} não pôde ser concluída: {completion.Error!.Code}.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        logger.LogInformation("Transfer {TransferId} completed with amount {Amount}", transfer.Id, transfer.Amount);
    }

    private async Task<string?> EvaluateBusinessRulesAsync(
        Account source,
        Account destination,
        Transfer transfer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (source.Status != AccountStatus.Active)
        {
            return source.Status == AccountStatus.Blocked ? "account.source_blocked" : "account.source_inactive";
        }

        if (destination.Status != AccountStatus.Active)
        {
            return destination.Status == AccountStatus.Blocked ? "account.destination_blocked" : "account.destination_inactive";
        }

        var policy = await dbContext.TransferLimitPolicies
            .FromSqlInterpolated($"SELECT * FROM \"transfer_limit_policies\" WHERE \"AccountId\" = {source.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        var limitFailure = await limitEvaluator.EvaluateAsync(
            policy,
            transfer.Id,
            source.Id,
            transfer.Amount,
            now,
            cancellationToken);
        if (limitFailure is not null)
        {
            return limitFailure;
        }

        var debitValidation = source.ValidateDebit(transfer.Amount);
        if (!debitValidation.IsSuccess)
        {
            return debitValidation.Error!.Code;
        }

        var creditValidation = destination.ValidateCredit(transfer.Amount);
        return creditValidation.IsSuccess ? null : creditValidation.Error!.Code;
    }

    private static void FailTransfer(Transfer transfer, string failureCode, DateTimeOffset now)
    {
        var failure = transfer.Fail(failureCode, now);
        if (!failure.IsSuccess)
        {
            throw new InvalidOperationException($"A transferência {transfer.Id} não pôde registrar a falha: {failure.Error!.Code}.");
        }
    }

}

/// <summary>Indica que uma mensagem agendada chegou à fila antes do horário persistido.</summary>
public sealed class TransferNotReadyException(Guid transferId, DateTimeOffset? scheduledAt)
    : InvalidOperationException($"A transferência {transferId} está disponível a partir de {scheduledAt:O}.");
