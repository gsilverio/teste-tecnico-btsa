using TesteTecnico.Api.Infrastructure.Endpoints;
using TesteTecnico.Api.Features.TransferLimits.Shared;

namespace TesteTecnico.Api.Features.TransferLimits.GetPolicy;

/// <summary>Endpoint de consulta de política.</summary>
public static class GetTransferLimitPolicyEndpoints
{
    /// <summary>Registra a consulta de política por identificador.</summary>
    public static RouteGroupBuilder MapGetTransferLimitPolicyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transfer-limit-policies/{policyId:guid}", async (
                Guid policyId,
                GetTransferLimitPolicyQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(policyId, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetTransferLimitPolicy")
            .WithTags("Transfer limits")
            .WithSummary("Consulta uma política de limite")
            .Produces<TransferLimitPolicyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
