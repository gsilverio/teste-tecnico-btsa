using TesteTecnico.Api.Infrastructure.Endpoints;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;

namespace TesteTecnico.Api.Features.TransferLimits.DeletePolicy;

/// <summary>Endpoint de exclusão de política.</summary>
public static class DeleteTransferLimitPolicyEndpoints
{
    /// <summary>Registra a exclusão de uma política de limite.</summary>
    public static RouteGroupBuilder MapDeleteTransferLimitPolicyEndpoints(this RouteGroupBuilder group)
    {
        group.MapDelete("/transfer-limit-policies/{policyId:guid}", async (
                Guid policyId,
                DeleteTransferLimitPolicyCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(policyId, cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    error => error.ToHttpResult(httpContext));
            })
            .WithName("DeleteTransferLimitPolicy")
            .WithTags("Transfer limits")
            .WithSummary("Remove uma política de limite")
            .Audit(AuditActionType.DeleteTransferLimitPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
