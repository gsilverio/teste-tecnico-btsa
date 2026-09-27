namespace TesteTecnico.Api.Common.Results;

/// <summary>Representa uma operação concluída sem valor de retorno.</summary>
public readonly record struct Unit
{
    /// <summary>Obtém a única instância de valor unitário.</summary>
    public static Unit Value => default;
}
