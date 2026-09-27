namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Registra um fato do domínio durante a mudança de estado de uma transferência.</summary>
public sealed record TransferStateChangedDomainEvent(
    Guid EventId,
    Guid TransferId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    TransferMethod Method,
    TransferStatus? PreviousStatus,
    TransferStatus Status,
    TransferAuditAction Action,
    int Sequence,
    DateTimeOffset OccurredAt,
    string? FailureCode);
