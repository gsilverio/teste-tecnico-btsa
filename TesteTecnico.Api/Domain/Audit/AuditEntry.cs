using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Domain.Audit;

/// <summary>Registro imutável de uma ação HTTP ou fato do domínio.</summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    /// <summary>Identificador único da linha de auditoria.</summary>
    public Guid Id { get; private set; }

    /// <summary>Origem da ação registrada.</summary>
    public AuditSource Source { get; private set; }

    /// <summary>Tipo da ação ou fato registrado.</summary>
    public AuditActionType ActionType { get; private set; }

    /// <summary>Identificador do usuário autenticado, quando a API fornecer essa identidade.</summary>
    public string? ActorId { get; private set; }

    /// <summary>Método HTTP para registros originados na API.</summary>
    public string? HttpMethod { get; private set; }

    /// <summary>Modelo da rota consultada, sem valores concretos de identificadores.</summary>
    public string? RouteTemplate { get; private set; }

    /// <summary>Código HTTP retornado para registros originados na API.</summary>
    public int? StatusCode { get; private set; }

    /// <summary>Identificador da transferência nos registros associados a uma transferência.</summary>
    public Guid? TransferId { get; private set; }

    /// <summary>Identificador da conta de origem registrado no fato do domínio.</summary>
    public Guid? SourceAccountId { get; private set; }

    /// <summary>Identificador da conta de destino registrado no fato do domínio.</summary>
    public Guid? DestinationAccountId { get; private set; }

    /// <summary>Valor da transferência registrado no fato do domínio.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Modalidade registrada no fato do domínio.</summary>
    public TransferMethod? Method { get; private set; }

    /// <summary>Ordem da mudança dentro do ciclo de vida da transferência.</summary>
    public int? Sequence { get; private set; }

    /// <summary>Estado anterior, nulo quando a solicitação foi criada.</summary>
    public TransferStatus? PreviousStatus { get; private set; }

    /// <summary>Estado resultante da mudança de domínio.</summary>
    public TransferStatus? Status { get; private set; }

    /// <summary>Instante UTC em que a ação foi registrada.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Dados compactos e sem conteúdo sensível, em JSON.</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>Motivo da falha de negócio ou detalhe de um ProblemDetails.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Cria uma linha de auditoria a partir de um fato do domínio.</summary>
    public static AuditEntry FromDomainEvent(TransferStateChangedDomainEvent domainEvent) => new()
    {
        Id = domainEvent.EventId,
        Source = AuditSource.DomainEvent,
        ActionType = MapAction(domainEvent.Action),
        TransferId = domainEvent.TransferId,
        SourceAccountId = domainEvent.SourceAccountId,
        DestinationAccountId = domainEvent.DestinationAccountId,
        Amount = domainEvent.Amount,
        Method = domainEvent.Method,
        Sequence = domainEvent.Sequence,
        PreviousStatus = domainEvent.PreviousStatus,
        Status = domainEvent.Status,
        OccurredAt = domainEvent.OccurredAt.ToUniversalTime(),
        ErrorMessage = domainEvent.FailureCode
    };

    /// <summary>Cria uma linha de auditoria para uma requisição HTTP anotada.</summary>
    public static AuditEntry FromApiRequest(
        AuditActionType actionType,
        string? actorId,
        string httpMethod,
        string routeTemplate,
        int statusCode,
        Guid? transferId,
        string payloadJson,
        string? errorMessage,
        DateTimeOffset occurredAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Source = AuditSource.Api,
        ActionType = actionType,
        ActorId = actorId,
        HttpMethod = httpMethod,
        RouteTemplate = routeTemplate,
        StatusCode = statusCode,
        TransferId = transferId,
        PayloadJson = payloadJson,
        ErrorMessage = errorMessage,
        OccurredAt = occurredAt.ToUniversalTime()
    };

    private static AuditActionType MapAction(TransferAuditAction action) => action switch
    {
        TransferAuditAction.Requested => AuditActionType.TransferRequested,
        TransferAuditAction.Scheduled => AuditActionType.TransferScheduled,
        TransferAuditAction.ProcessingStarted => AuditActionType.TransferProcessingStarted,
        TransferAuditAction.Completed => AuditActionType.TransferCompleted,
        TransferAuditAction.Failed => AuditActionType.TransferFailed,
        TransferAuditAction.Cancelled => AuditActionType.TransferCancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Ação de auditoria de transferência desconhecida.")
    };
}
