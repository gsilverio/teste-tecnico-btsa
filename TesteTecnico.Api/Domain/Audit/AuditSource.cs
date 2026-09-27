namespace TesteTecnico.Api.Domain.Audit;

/// <summary>Origem de uma linha do histórico de auditoria.</summary>
public enum AuditSource
{
    /// <summary>Requisição HTTP anotada no endpoint.</summary>
    Api,

    /// <summary>Evento emitido pelo domínio de transferências.</summary>
    DomainEvent
}
