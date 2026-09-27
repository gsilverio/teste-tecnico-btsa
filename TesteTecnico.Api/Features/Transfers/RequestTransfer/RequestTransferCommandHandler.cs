using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Features.Transfers.ExecuteTransfer;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Infrastructure.Jobs;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.RequestTransfer;

/// <summary>Executa transferências imediatas e persiste/agendas as solicitações futuras.</summary>
public sealed class RequestTransferCommandHandler(
    AppDbContext dbContext,
    IBackgroundJobClient backgroundJobClient,
    ExecuteTransferCommandHandler transferExecutor,
    TimeProvider timeProvider,
    ILogger<RequestTransferCommandHandler> logger)
{
    /// <summary>Processa uma transferência imediata antes de responder ou agenda uma futura, com idempotência.</summary>
    public async Task<Result<TransferResponse>> HandleAsync(
        TransferRequest request,
        string? idempotencyKey,
        bool scheduled,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey, out var keyError);
        if (keyError is not null)
        {
            return Result<TransferResponse>.Failure(keyError);
        }

        if (request.SourceAccountId == Guid.Empty)
        {
            return Failure("account.not_found", "A conta de origem não foi encontrada.");
        }

        var methodResult = ParseMethod(request.Method);
        if (!methodResult.IsSuccess)
        {
            return Result<TransferResponse>.Failure(methodResult.Error!);
        }

        var method = methodResult.Value;
        var destinationResult = await ResolveDestinationAsync(request, method, cancellationToken);
        if (!destinationResult.IsSuccess)
        {
            return Result<TransferResponse>.Failure(destinationResult.Error!);
        }

        var now = timeProvider.GetUtcNow();
        var scheduledAt = scheduled ? request.ScheduledAt : null;
        if (scheduled && scheduledAt is null)
        {
            return Failure("transfer.invalid_schedule", "A data de execução agendada é obrigatória.");
        }

        if (!scheduled && request.ScheduledAt is not null)
        {
            return Failure("transfer.invalid_schedule", "A rota de transferência imediata não aceita uma data agendada.");
        }

        var fingerprint = normalizedKey is null
            ? null
            : CreateFingerprint(request.SourceAccountId, destinationResult.Value, method, request.Amount, scheduledAt);

        if (scheduled)
        {
            var transferResult = Transfer.Schedule(
                request.SourceAccountId,
                destinationResult.Value,
                request.Amount,
                method,
                now,
                scheduledAt!.Value,
                normalizedKey,
                fingerprint);
            if (!transferResult.IsSuccess)
            {
                return Result<TransferResponse>.Failure(transferResult.Error!);
            }

            var persistence = await PersistTransferAsync(
                transferResult.Value,
                request.SourceAccountId,
                normalizedKey,
                fingerprint,
                now,
                scheduledAt.Value,
                cancellationToken);
            if (!persistence.IsSuccess)
            {
                return Result<TransferResponse>.Failure(persistence.Error!);
            }

            var transfer = persistence.Value;
            await EnsureDispatchScheduledAsync(transfer, cancellationToken);
            var scheduledTransfer = await dbContext.Transfers.AsNoTracking()
                .SingleAsync(item => item.Id == transfer.Id, cancellationToken);
            logger.LogInformation(
                "Scheduled transfer {TransferId} persisted with status {TransferStatus} for {ScheduledAt}",
                transfer.Id,
                scheduledTransfer.Status,
                scheduledTransfer.ScheduledAt);
            return Result<TransferResponse>.Success(ToTransferResponse(scheduledTransfer));
        }

        var immediateTransferResult = Transfer.CreateImmediate(
            request.SourceAccountId,
            destinationResult.Value,
            request.Amount,
            method,
            now,
            normalizedKey,
            fingerprint);
        if (!immediateTransferResult.IsSuccess)
        {
            return Result<TransferResponse>.Failure(immediateTransferResult.Error!);
        }

        return await ExecuteImmediateTransferAsync(
            immediateTransferResult.Value,
            request.SourceAccountId,
            normalizedKey,
            fingerprint,
            cancellationToken);
    }

    private async Task<Result<Transfer>> PersistTransferAsync(
        Transfer transfer,
        Guid sourceAccountId,
        string? normalizedKey,
        string? fingerprint,
        DateTimeOffset now,
        DateTimeOffset availableAt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PersistTransferInTransactionAsync(
                transfer,
                sourceAccountId,
                normalizedKey,
                fingerprint,
                now,
                availableAt,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (normalizedKey is not null && IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentRequest = await dbContext.Transfers.SingleOrDefaultAsync(
                item => item.SourceAccountId == sourceAccountId && item.IdempotencyKey == normalizedKey,
                cancellationToken);
            if (concurrentRequest is null)
            {
                throw;
            }

            return string.Equals(concurrentRequest.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Result<Transfer>.Success(concurrentRequest)
                : Result<Transfer>.Failure(new Error(
                    "transfer.idempotency_conflict",
                    "A chave de idempotência já foi usada com dados diferentes."));
        }
    }

    private async Task<Result<Transfer>> PersistTransferInTransactionAsync(
        Transfer transfer,
        Guid sourceAccountId,
        string? normalizedKey,
        string? fingerprint,
        DateTimeOffset now,
        DateTimeOffset availableAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = normalizedKey is null
            ? null
            : await dbContext.Transfers.SingleOrDefaultAsync(
                item => item.SourceAccountId == sourceAccountId && item.IdempotencyKey == normalizedKey,
                cancellationToken);

        if (existing is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Result<Transfer>.Success(existing)
                : Result<Transfer>.Failure(new Error(
                    "transfer.idempotency_conflict",
                    "A chave de idempotência já foi usada com dados diferentes."));
        }

        var outboxMessage = TransferOutboxMessage.Create(transfer.Id, availableAt, now);
        dbContext.Transfers.Add(transfer);
        dbContext.TransferOutboxMessages.Add(outboxMessage);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Transfer>.Success(transfer);
    }

    private async Task<Result<TransferResponse>> ExecuteImmediateTransferAsync(
        Transfer transfer,
        Guid sourceAccountId,
        string? normalizedKey,
        string? fingerprint,
        CancellationToken cancellationToken)
    {
        var existing = normalizedKey is null
            ? null
            : await dbContext.Transfers.AsNoTracking().SingleOrDefaultAsync(
                item => item.SourceAccountId == sourceAccountId && item.IdempotencyKey == normalizedKey,
                cancellationToken);

        if (existing is not null)
        {
            return await ExecuteExistingImmediateTransferAsync(existing, fingerprint, cancellationToken);
        }

        try
        {
            return await CreateAndExecuteImmediateTransferAsync(transfer, cancellationToken);
        }
        catch (DbUpdateException exception) when (normalizedKey is not null && IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentRequest = await dbContext.Transfers.AsNoTracking().SingleOrDefaultAsync(
                item => item.SourceAccountId == sourceAccountId && item.IdempotencyKey == normalizedKey,
                cancellationToken);
            if (concurrentRequest is null)
            {
                throw;
            }

            return await ExecuteExistingImmediateTransferAsync(concurrentRequest, fingerprint, cancellationToken);
        }
    }

    private async Task<Result<TransferResponse>> CreateAndExecuteImmediateTransferAsync(
        Transfer transfer,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Transfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Keep persistence and financial execution in one transaction. If a technical failure
        // occurs before completion, both the new transfer and its ledger effects roll back.
        await transferExecutor.HandleAsync(transfer.Id, CancellationToken.None);
        var finalTransfer = await dbContext.Transfers.AsNoTracking()
            .SingleAsync(item => item.Id == transfer.Id, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        logger.LogInformation(
            "Immediate transfer {TransferId} completed synchronously with status {TransferStatus}",
            transfer.Id,
            finalTransfer.Status);
        return Result<TransferResponse>.Success(ToTransferResponse(finalTransfer));
    }

    private async Task<Result<TransferResponse>> ExecuteExistingImmediateTransferAsync(
        Transfer existing,
        string? fingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Failure("transfer.idempotency_conflict", "A chave de idempotência já foi usada com dados diferentes.");
        }

        await transferExecutor.HandleAsync(existing.Id, CancellationToken.None);
        var current = await dbContext.Transfers.AsNoTracking()
            .SingleAsync(item => item.Id == existing.Id, cancellationToken);
        return Result<TransferResponse>.Success(ToTransferResponse(current));
    }

    private async Task<Result<Guid>> ResolveDestinationAsync(
        TransferRequest request,
        TransferMethod method,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Accounts.AnyAsync(account => account.Id == request.SourceAccountId, cancellationToken))
        {
            return Result<Guid>.Failure(new Error("account.not_found", "A conta de origem não foi encontrada."));
        }

        if (method == TransferMethod.Pix)
        {
            if (string.IsNullOrWhiteSpace(request.PixKey) || request.BankIspb is not null ||
                request.Branch is not null || request.AccountNumber is not null || request.CheckDigit is not null)
            {
                return Result<Guid>.Failure(new Error("transfer.invalid_destination", "Informe somente uma chave Pix válida para este tipo de transferência."));
            }

            var normalizedKeys = Enum.GetValues<PixKeyType>()
                .Select(type => PixKey.Create(type, request.PixKey))
                .Where(result => result.IsSuccess)
                .Select(result => result.Value.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (normalizedKeys.Length == 0)
            {
                return Result<Guid>.Failure(new Error("transfer.invalid_destination", "A chave Pix informada é inválida."));
            }

            var accountId = await dbContext.Accounts.AsNoTracking()
                .Where(account => account.PixKeys.Any(key => normalizedKeys.Contains(key.Value)))
                .Select(account => (Guid?)account.Id)
                .SingleOrDefaultAsync(cancellationToken);

            return accountId is null
                ? Result<Guid>.Failure(new Error("account.not_found", "Não foi encontrada uma conta para a chave Pix informada."))
                : Result<Guid>.Success(accountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.PixKey) || string.IsNullOrWhiteSpace(request.BankIspb) ||
            string.IsNullOrWhiteSpace(request.Branch) || string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            return Result<Guid>.Failure(new Error("transfer.invalid_destination", "Informe banco, agência e conta sem preencher uma chave Pix."));
        }

        var query =
            from account in dbContext.Accounts.AsNoTracking()
            join bank in dbContext.Banks.AsNoTracking() on account.BankId equals bank.Id
            where bank.Ispb == request.BankIspb.Trim()
                && account.Branch == request.Branch.Trim()
                && account.Number == request.AccountNumber.Trim()
            select new { account.Id, account.CheckDigit };

        if (request.CheckDigit is not null)
        {
            query = query.Where(item => item.CheckDigit == request.CheckDigit.Trim());
        }

        var destinationId = await query.Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        return destinationId is null
            ? Result<Guid>.Failure(new Error("account.not_found", "Não foi encontrada uma conta para os dados bancários informados."))
            : Result<Guid>.Success(destinationId.Value);
    }

    private async Task EnsureDispatchScheduledAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        if (transfer.Status is TransferStatus.Completed or TransferStatus.Failed or TransferStatus.Cancelled)
        {
            return;
        }

        var message = await dbContext.TransferOutboxMessages
            .SingleOrDefaultAsync(item => item.TransferId == transfer.Id, cancellationToken);
        if (message is null)
        {
            message = TransferOutboxMessage.Create(
                transfer.Id,
                transfer.ScheduledAt ?? transfer.CreatedAt,
                timeProvider.GetUtcNow());
            dbContext.TransferOutboxMessages.Add(message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (message.PublishedAt is null)
        {
            ScheduleDispatch(message);
        }
    }

    private void ScheduleDispatch(TransferOutboxMessage message)
    {
        try
        {
            if (message.AvailableAt > timeProvider.GetUtcNow())
            {
                backgroundJobClient.Schedule<TransferDispatchJob>(
                    job => job.DispatchAsync(message.Id, CancellationToken.None),
                    message.AvailableAt);
            }
            else
            {
                backgroundJobClient.Enqueue<TransferDispatchJob>(
                    job => job.DispatchAsync(message.Id, CancellationToken.None));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Could not schedule RabbitMQ dispatch for transfer {TransferId}; outbox recovery will retry it",
                message.TransferId);
        }
    }

    private static string? NormalizeIdempotencyKey(string? key, out Error? error)
    {
        error = null;
        if (key is null)
        {
            return null;
        }

        var normalized = key.Trim();
        if (normalized.Length is 0 or > 200)
        {
            error = new Error("transfer.invalid_idempotency_key", "A chave de idempotência deve conter de 1 a 200 caracteres.");
            return null;
        }

        return normalized;
    }

    private static Result<TransferMethod> ParseMethod(string? method) => method switch
    {
        "Pix" => Result<TransferMethod>.Success(TransferMethod.Pix),
        "BankAccount" => Result<TransferMethod>.Success(TransferMethod.BankAccount),
        _ => Result<TransferMethod>.Failure(new Error("transfer.invalid_method", "A modalidade deve ser Pix ou BankAccount."))
    };

    private static string CreateFingerprint(
        Guid sourceAccountId,
        Guid destinationAccountId,
        TransferMethod method,
        decimal amount,
        DateTimeOffset? scheduledAt)
    {
        var requestData = string.Join('|',
            sourceAccountId.ToString("D"),
            destinationAccountId.ToString("D"),
            method.ToString(),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            scheduledAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestData))).ToLowerInvariant();
    }

    private static TransferResponse ToTransferResponse(Transfer transfer) =>
        new(
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
            transfer.FailureCode);

    private static Result<TransferResponse> Failure(string code, string message) =>
        Result<TransferResponse>.Failure(new Error(code, message));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
