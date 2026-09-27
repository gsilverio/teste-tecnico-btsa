using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.TransferLimits.UpdatePolicy;

/// <summary>Endpoint de atualização de política.</summary>
public static class UpdateTransferLimitPolicyEndpoints
{
    /// <summary>Registra a atualização dos limites de uma política.</summary>
    public static RouteGroupBuilder MapUpdateTransferLimitPolicyEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/transfer-limit-policies/{policyId:guid}", async (
                Guid policyId,
                TransferLimitPolicyValuesRequest request,
                UpdateTransferLimitPolicyCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(policyId, request, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("UpdateTransferLimitPolicy")
            .WithTags("Transfer limits")
            .WithSummary("Atualiza os limites de uma política")
            .WithDescription("Zero desabilita transferências no período correspondente. Valores monetários precisam ter no máximo duas casas decimais.")
            .Audit(AuditActionType.UpdateTransferLimitPolicy)
            .Produces<TransferLimitPolicyResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
