namespace TesteTecnico.Api.Common.Results;

/// <summary>Oferece operações comuns para resultados sem valor de retorno.</summary>
public static class ResultExtensions
{
    /// <summary>Cria um resultado de sucesso sem valor de retorno.</summary>
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    /// <summary>Cria um resultado de falha sem valor de retorno.</summary>
    /// <param name="error">Erro esperado da operação.</param>
    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);
}
