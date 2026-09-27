using TesteTecnico.Api.Infrastructure.Endpoints;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;

namespace TesteTecnico.Api.Features.Accounts.ManageOverdraft;

/// <summary>Rotas de consulta, alteração e remoção lógica do limite de cheque especial.</summary>
public static class ManageOverdraftEndpoints
{
    /// <summary>Registra as rotas de cheque especial.</summary>
    public static RouteGroupBuilder MapManageOverdraftEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/accounts/{accountId:guid}/overdraft-limit", async (
                Guid accountId,
                GetOverdraftLimitQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(accountId, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("GetAccountOverdraftLimit")
            .WithTags("Accounts")
            .WithSummary("Consulta o cheque especial da conta")
            .Audit(AuditActionType.ViewAccountOverdraft)
            .Produces<OverdraftLimitResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/accounts/{accountId:guid}/overdraft-limit", async (
                Guid accountId,
                SetOverdraftLimitRequest request,
                SetOverdraftLimitCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(accountId, request.Limit, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("SetAccountOverdraftLimit")
            .WithTags("Accounts")
            .WithSummary("Altera o cheque especial da conta")
            .WithDescription("O novo limite não pode ser negativo nem inferior ao cheque especial que já está sendo utilizado.")
            .Audit(AuditActionType.SetAccountOverdraft)
            .Produces<OverdraftLimitResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/accounts/{accountId:guid}/overdraft-limit", async (
                Guid accountId,
                SetOverdraftLimitCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(accountId, 0m, cancellationToken);
                return result.Match<IResult>(response => Results.Ok(response), error => error.ToHttpResult(httpContext));
            })
            .WithName("ClearAccountOverdraftLimit")
            .WithTags("Accounts")
            .WithSummary("Remove o cheque especial da conta")
            .WithDescription("Define o limite como zero; a remoção falha se a conta estiver utilizando o cheque especial.")
            .Audit(AuditActionType.ClearAccountOverdraft)
            .Produces<OverdraftLimitResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
