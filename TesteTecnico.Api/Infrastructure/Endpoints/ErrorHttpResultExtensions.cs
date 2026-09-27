using Microsoft.AspNetCore.Mvc;
using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Infrastructure.Endpoints;

/// <summary>Converte erros esperados para respostas HTTP ProblemDetails.</summary>
public static class ErrorHttpResultExtensions
{
    /// <summary>Mapeia um erro de aplicação para seu status HTTP sem vazar detalhes internos.</summary>
    public static IResult ToHttpResult(this Error error, HttpContext httpContext)
    {
        var statusCode = error.Code.EndsWith(".not_found", StringComparison.Ordinal)
            ? StatusCodes.Status404NotFound
            : error.Code is "transfer_limit_policy.already_exists"
                or "account.overdraft_below_balance"
                or "account_holder.cpf_conflict"
                or "account.already_exists"
                or "transfer.idempotency_conflict"
                or "transfer.invalid_state"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Recurso não encontrado.",
                StatusCodes.Status409Conflict => "Conflito com o estado atual do recurso.",
                _ => "A solicitação é inválida."
            },
            Detail = error.Message
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        return Results.Problem(problem);
    }
}
