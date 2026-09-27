using TesteTecnico.Api.Domain.Audit;

namespace TesteTecnico.Api.Infrastructure.Audit;

/// <summary>Metadado opt-in que marca uma ação HTTP para auditoria.</summary>
public sealed class AuditActionAttribute(AuditActionType actionType) : Attribute
{
    /// <summary>Tipo de ação associado ao endpoint.</summary>
    public AuditActionType ActionType { get; } = actionType;
}
