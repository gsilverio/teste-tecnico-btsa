using TesteTecnico.Api.Infrastructure.Endpoints;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Infrastructure.Audit;

namespace TesteTecnico.Api.Features.Accounts.ManageStatus;

/// <summary>Rotas para bloquear, inativar e reativar contas durante os testes.</summary>
public static class ManageAccountStatusEndpoints
{
    /// <summary>Registra a rota de alteração de situação da conta.</summary>
    public static RouteGroupBuilder MapManageAccountStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/accounts/{accountId:guid}/status", async (
                Guid accountId,
                SetAccountStatusRequest request,
                ManageAccountStatusCommandHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(accountId, request.Status, cancellationToken);
                return result.Match<IResult>(Results.Ok, error => error.ToHttpResult(httpContext));
            })
            .WithName("SetAccountStatus")
            .WithTags("Accounts")
            .WithSummary("Altera a situação de uma conta")
            .WithDescription("Bloqueia, inativa ou reativa a conta; a alteração é serializada com transferências em andamento.")
            .Audit(AuditActionType.SetAccountStatus)
            .Produces<AccountStatusResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
