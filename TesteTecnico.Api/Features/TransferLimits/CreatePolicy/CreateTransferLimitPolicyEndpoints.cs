using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.TransferLimits.CreatePolicy;

/// <summary>Endpoint de criação de políticas de limite.</summary>
public static class CreateTransferLimitPolicyEndpoints
{
    /// <summary>Registra a criação de política por conta.</summary>
    public static RouteGroupBuilder MapCreateTransferLimitPolicyEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/transfer-limit-policies", async (
                CreateTransferLimitPolicyRequest request,
                CreateTransferLimitPolicyCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);
                return result.Match<IResult>(
                    response => Results.Created($"/api/transfer-limit-policies/{response.Id}", response),
                    error => error.ToHttpResult(httpContext));
            })
            .WithName("CreateTransferLimitPolicy")
            .WithTags("Transfer limits")
            .WithSummary("Cria os limites de transferência de uma conta")
            .WithDescription("Cada conta pode ter uma política. Os valores podem ser zero para bloquear transferências naquele período.")
            .Audit(AuditActionType.CreateTransferLimitPolicy)
            .Produces<TransferLimitPolicyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
