using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Infrastructure.Endpoints;

namespace TesteTecnico.Api.Features.TransferLimits.GetPolicies;

/// <summary>Endpoint de listagem de políticas.</summary>
public static class GetTransferLimitPoliciesEndpoints
{
    /// <summary>Registra a listagem paginada.</summary>
    public static RouteGroupBuilder MapGetTransferLimitPoliciesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transfer-limit-policies", async (
                int? page,
                int? pageSize,
                GetTransferLimitPoliciesQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var pagination = PaginationRequest.FromQuery(page, pageSize);
                if (pagination.ToHttpResultIfInvalid(httpContext, "transfer_limit_policy.invalid_pagination") is { } invalidPagination)
                {
                    return invalidPagination;
                }

                var result = await handler.HandleAsync(pagination, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetTransferLimitPolicies")
            .WithTags("Transfer limits")
            .WithSummary("Lista as políticas de limite")
            .WithDescription("Retorna as políticas de transferência associadas às contas.")
            .Produces<Page<TransferLimitPolicyResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}
