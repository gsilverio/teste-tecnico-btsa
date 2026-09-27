using TesteTecnico.Api.Domain.Audit;

namespace TesteTecnico.Api.Infrastructure.Audit;

/// <summary>Extensões para declarar quais endpoints geram auditoria de requisição.</summary>
public static class AuditEndpointExtensions
{
    /// <summary>Anota uma rota com a ação auditável correspondente.</summary>
    public static RouteHandlerBuilder Audit(this RouteHandlerBuilder builder, AuditActionType actionType)
    {
        builder.WithMetadata(new AuditActionAttribute(actionType));
        return builder;
    }
}
