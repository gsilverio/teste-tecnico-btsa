using TesteTecnico.Api.Common.Pagination;

namespace TesteTecnico.Api.Infrastructure.Endpoints;

/// <summary>Adapta erros de paginação para respostas HTTP Problem Details.</summary>
public static class PaginationHttpResultExtensions
{
    /// <summary>Retorna um erro HTTP quando os parâmetros de paginação são inválidos.</summary>
    /// <param name="pagination">Parâmetros recebidos pela rota.</param>
    /// <param name="httpContext">Contexto usado para correlacionar a resposta.</param>
    /// <param name="errorCode">Código estável específico do recurso.</param>
    /// <returns>Um problema HTTP 400 ou <see langword="null"/> quando os parâmetros são válidos.</returns>
    public static IResult? ToHttpResultIfInvalid(
        this PaginationRequest pagination,
        HttpContext httpContext,
        string errorCode)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        if (pagination.IsValid)
        {
            return null;
        }

        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Parâmetros de paginação inválidos.",
            detail: $"page deve estar entre {PaginationRequest.DefaultPage} e {PaginationRequest.MaximumPage} e pageSize deve estar entre 1 e {PaginationRequest.MaximumPageSize}.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["traceId"] = httpContext.TraceIdentifier
            });
    }
}
