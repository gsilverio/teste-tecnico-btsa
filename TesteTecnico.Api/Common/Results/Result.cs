namespace TesteTecnico.Api.Common.Results;

/// <summary>Representa o sucesso ou a falha esperada de uma operação.</summary>
/// <typeparam name="T">Tipo do valor produzido em caso de sucesso.</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
    }

    /// <summary>Indica se a operação foi concluída com sucesso.</summary>
    public bool IsSuccess { get; }

    /// <summary>Obtém o valor produzido; lança exceção se o resultado for uma falha.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Um resultado com falha não possui valor.");

    /// <summary>Obtém o erro quando a operação falha.</summary>
    public Error? Error { get; }

    /// <summary>Cria um resultado de sucesso.</summary>
    /// <param name="value">Valor produzido pela operação.</param>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Cria um resultado de falha.</summary>
    /// <param name="error">Erro esperado da operação.</param>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>Executa a função correspondente ao estado do resultado.</summary>
    /// <typeparam name="TOutput">Tipo produzido pelas funções.</typeparam>
    /// <param name="onSuccess">Função chamada quando há sucesso.</param>
    /// <param name="onFailure">Função chamada quando há falha.</param>
    public TOutput Match<TOutput>(Func<T, TOutput> onSuccess, Func<Error, TOutput> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error!);
}
