using TesteTecnico.Api.Common.Results;
using System.ComponentModel.DataAnnotations.Schema;

namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Registra uma transferência imediata ou agendada entre duas contas.</summary>
public sealed class Transfer
{
    private const decimal MaximumMoney = 9_999_999_999_999_999.99m;
    private readonly List<TransferStateChangedDomainEvent> _domainEvents = [];

    private Transfer()
    {
    }

    private Transfer(
        Guid id,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        TransferMethod method,
        TransferStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? scheduledAt,
        string? idempotencyKey,
        string? requestFingerprint)
    {
        Id = id;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        Method = method;
        Status = status;
        CreatedAt = createdAt;
        ScheduledAt = scheduledAt;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
    }

    /// <summary>Identificador único da transferência.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador da conta debitada.</summary>
    public Guid SourceAccountId { get; private set; }

    /// <summary>Identificador da conta creditada.</summary>
    public Guid DestinationAccountId { get; private set; }

    /// <summary>Valor transferido em reais.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Modalidade informada para identificar o destino.</summary>
    public TransferMethod Method { get; private set; }

    /// <summary>Estado atual da transferência.</summary>
    public TransferStatus Status { get; private set; }

    /// <summary>Instante em que a solicitação foi criada, em UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Instante futuro programado, ou nulo para uma transferência imediata.</summary>
    public DateTimeOffset? ScheduledAt { get; private set; }

    /// <summary>Chave opcional usada pelo cliente para repetir uma solicitação com segurança.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Hash dos dados normalizados associados à chave de idempotência.</summary>
    public string? RequestFingerprint { get; private set; }

    /// <summary>Instante em que a execução atingiu um estado terminal, em UTC.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Instante do cancelamento, em UTC; permanece registrado junto com a transferência.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Código estável que explica uma falha de execução, quando houver.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Sequência do último fato de domínio para ordenar o histórico da transferência.</summary>
    public int AuditSequence { get; private set; }

    /// <summary>Fatos de domínio ainda não persistidos junto com a transferência.</summary>
    [NotMapped]
    public IReadOnlyCollection<TransferStateChangedDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Remove os fatos já persistidos pelo contexto de banco.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Cria uma transferência imediata em processamento na transação da requisição.</summary>
    /// <param name="sourceAccountId">Conta de origem.</param>
    /// <param name="destinationAccountId">Conta de destino.</param>
    /// <param name="amount">Valor em reais.</param>
    /// <param name="method">Modalidade da transferência.</param>
    /// <param name="createdAt">Instante da solicitação.</param>
    public static Result<Transfer> CreateImmediate(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        TransferMethod method,
        DateTimeOffset createdAt,
        string? idempotencyKey = null,
        string? requestFingerprint = null)
    {
        var validation = ValidateRequest(sourceAccountId, destinationAccountId, amount, method);
        validation ??= ValidateIdempotency(idempotencyKey, requestFingerprint);
        if (validation is not null)
        {
            return Result<Transfer>.Failure(validation);
        }

        var utcCreatedAt = createdAt.ToUniversalTime();
        var transfer = new Transfer(
            Guid.CreateVersion7(),
            sourceAccountId,
            destinationAccountId,
            amount,
            method,
            TransferStatus.Processing,
            utcCreatedAt,
            null,
            idempotencyKey,
            requestFingerprint);
        transfer.RecordStateChange(TransferAuditAction.Requested, null, utcCreatedAt);
        return Result<Transfer>.Success(transfer);
    }

    /// <summary>Cria uma transferência para uma data futura.</summary>
    /// <param name="sourceAccountId">Conta de origem.</param>
    /// <param name="destinationAccountId">Conta de destino.</param>
    /// <param name="amount">Valor em reais.</param>
    /// <param name="method">Modalidade da transferência.</param>
    /// <param name="createdAt">Instante da solicitação.</param>
    /// <param name="scheduledAt">Instante futuro em que a transferência poderá ser executada.</param>
    public static Result<Transfer> Schedule(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        TransferMethod method,
        DateTimeOffset createdAt,
        DateTimeOffset scheduledAt,
        string? idempotencyKey = null,
        string? requestFingerprint = null)
    {
        var validation = ValidateRequest(sourceAccountId, destinationAccountId, amount, method);
        validation ??= ValidateIdempotency(idempotencyKey, requestFingerprint);
        if (validation is not null)
        {
            return Result<Transfer>.Failure(validation);
        }

        if (scheduledAt <= createdAt)
        {
            return Result<Transfer>.Failure(new Error("transfer.invalid_schedule", "A data de execução deve ser futura."));
        }

        var utcCreatedAt = createdAt.ToUniversalTime();
        var transfer = new Transfer(
            Guid.CreateVersion7(),
            sourceAccountId,
            destinationAccountId,
            amount,
            method,
            TransferStatus.Scheduled,
            utcCreatedAt,
            scheduledAt.ToUniversalTime(),
            idempotencyKey,
            requestFingerprint);
        transfer.RecordStateChange(TransferAuditAction.Scheduled, null, utcCreatedAt);
        return Result<Transfer>.Success(transfer);
    }

    /// <summary>Conclui uma transferência que está em processamento.</summary>
    /// <param name="finishedAt">Instante de conclusão.</param>
    public Result<Unit> Complete(DateTimeOffset finishedAt)
    {
        if (Status != TransferStatus.Processing)
        {
            return InvalidTransition("Somente uma transferência em processamento pode ser concluída.");
        }

        var previousStatus = Status;
        var utcFinishedAt = finishedAt.ToUniversalTime();
        Status = TransferStatus.Completed;
        FinishedAt = utcFinishedAt;
        FailureCode = null;
        RecordStateChange(TransferAuditAction.Completed, previousStatus, utcFinishedAt);
        return ResultExtensions.Success();
    }

    /// <summary>Registra a falha de uma transferência em processamento.</summary>
    /// <param name="failureCode">Código estável da falha de negócio.</param>
    /// <param name="finishedAt">Instante em que a execução terminou.</param>
    public Result<Unit> Fail(string failureCode, DateTimeOffset finishedAt)
    {
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Trim().Length > 80)
        {
            return ResultExtensions.Failure(new Error("transfer.invalid_failure_code", "O código da falha deve conter de 1 a 80 caracteres."));
        }

        if (Status != TransferStatus.Processing)
        {
            return InvalidTransition("Somente uma transferência em processamento pode falhar.");
        }

        var previousStatus = Status;
        var utcFinishedAt = finishedAt.ToUniversalTime();
        Status = TransferStatus.Failed;
        FailureCode = failureCode.Trim();
        FinishedAt = utcFinishedAt;
        RecordStateChange(TransferAuditAction.Failed, previousStatus, utcFinishedAt, FailureCode);
        return ResultExtensions.Success();
    }

    /// <summary>Inicia uma transferência agendada quando sua data já foi atingida.</summary>
    /// <param name="startedAt">Instante de início da execução.</param>
    public Result<Unit> BeginScheduledProcessing(DateTimeOffset startedAt)
    {
        if (Status != TransferStatus.Scheduled)
        {
            return InvalidTransition("Somente uma transferência agendada pode iniciar a execução.");
        }

        if (ScheduledAt is null || startedAt < ScheduledAt.Value)
        {
            return InvalidTransition("A data programada da transferência ainda não foi atingida.");
        }

        var previousStatus = Status;
        var utcStartedAt = startedAt.ToUniversalTime();
        Status = TransferStatus.Processing;
        RecordStateChange(TransferAuditAction.ProcessingStarted, previousStatus, utcStartedAt);
        return ResultExtensions.Success();
    }

    /// <summary>Cancela uma transferência que ainda aguarda a execução agendada.</summary>
    /// <param name="cancelledAt">Instante do cancelamento.</param>
    public Result<Unit> Cancel(DateTimeOffset cancelledAt)
    {
        if (Status != TransferStatus.Scheduled)
        {
            return InvalidTransition("Somente uma transferência agendada pode ser cancelada.");
        }

        var previousStatus = Status;
        var utcCancelledAt = cancelledAt.ToUniversalTime();
        Status = TransferStatus.Cancelled;
        CancelledAt = utcCancelledAt;
        FinishedAt = CancelledAt;
        RecordStateChange(TransferAuditAction.Cancelled, previousStatus, utcCancelledAt);
        return ResultExtensions.Success();
    }

    /// <summary>Valida os dados financeiros sem aplicar regras temporais de uma nova solicitação.</summary>
    public static Error? ValidateRequest(Guid sourceAccountId, Guid destinationAccountId, decimal amount, TransferMethod method)
    {
        if (sourceAccountId == Guid.Empty || destinationAccountId == Guid.Empty || sourceAccountId == destinationAccountId)
        {
            return new Error("transfer.invalid_accounts", "A transferência precisa ter contas de origem e destino diferentes e válidas.");
        }

        if (amount <= 0m || amount > MaximumMoney || decimal.Round(amount, 2) != amount)
        {
            return new Error("transfer.invalid_amount", "O valor deve caber em numeric(18,2) e ter no máximo duas casas decimais.");
        }

        if (!Enum.IsDefined(method))
        {
            return new Error("transfer.invalid_method", "A modalidade informada é inválida.");
        }

        return null;
    }

    private static Error? ValidateIdempotency(string? idempotencyKey, string? requestFingerprint)
    {
        if (idempotencyKey is null && requestFingerprint is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200 ||
            requestFingerprint is null || requestFingerprint.Length != 64)
        {
            return new Error("transfer.invalid_idempotency_key", "A chave de idempotência ou sua impressão digital é inválida.");
        }

        return null;
    }

    private void RecordStateChange(
        TransferAuditAction action,
        TransferStatus? previousStatus,
        DateTimeOffset occurredAt,
        string? failureCode = null)
    {
        AuditSequence++;
        _domainEvents.Add(new TransferStateChangedDomainEvent(
            Guid.CreateVersion7(),
            Id,
            SourceAccountId,
            DestinationAccountId,
            Amount,
            Method,
            previousStatus,
            Status,
            action,
            AuditSequence,
            occurredAt.ToUniversalTime(),
            failureCode));
    }

    private static Result<Unit> InvalidTransition(string message) =>
        ResultExtensions.Failure(new Error("transfer.invalid_state", message));
}
