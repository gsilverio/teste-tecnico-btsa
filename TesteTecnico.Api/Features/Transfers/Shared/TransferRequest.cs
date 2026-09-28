namespace TesteTecnico.Api.Features.Transfers.Shared;

/// <summary>Dados comuns para solicitar uma transferência imediata ou agendada.</summary>
public sealed record TransferRequest(
    Guid SourceAccountId,
    string Method,
    decimal Amount,
    string? PixKey = null,
    string? BankIspb = null,
    string? Branch = null,
    string? AccountNumber = null,
    string? CheckDigit = null,
    DateTimeOffset? ScheduledAt = null);

/// <summary>Confirma a persistência de uma transferência agendada para processamento futuro.</summary>
public sealed record TransferAcceptedResponse(
    Guid Id,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt);

/// <summary>Dados públicos de uma transferência e seu estado atual.</summary>
public sealed record TransferResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Method,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? CancelledAt,
    string? FailureCode,
    IReadOnlyList<TransferAuditEventResponse>? AuditTrail = null,
    string? ProcessingError = null,
    bool IsInDeadLetter = false);

/// <summary>Fato persistido no histórico do ciclo de vida de uma transferência.</summary>
public sealed record TransferAuditEventResponse(
    Guid Id,
    string Action,
    int Sequence,
    string? PreviousStatus,
    string Status,
    DateTimeOffset OccurredAt,
    string? FailureCode);
/// <summary>Janela e horários usados na avaliação dos limites de transferência.</summary>
public sealed class TransferRulesOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "TransferRules";

    /// <summary>Duração da janela móvel de contagem.</summary>
    public int WindowMinutes { get; init; } = 60;

    /// <summary>Identificador IANA do fuso usado para selecionar período diurno ou noturno.</summary>
    public string TimeZoneId { get; init; } = "America/Sao_Paulo";

    /// <summary>Início inclusivo do período diurno no fuso configurado.</summary>
    public TimeOnly DayStart { get; init; } = new(6, 0);

    /// <summary>Fim exclusivo do período diurno no fuso configurado.</summary>
    public TimeOnly NightStart { get; init; } = new(22, 0);
}
