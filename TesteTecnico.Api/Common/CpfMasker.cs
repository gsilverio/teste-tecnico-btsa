namespace TesteTecnico.Api.Common;

/// <summary>Formata CPFs para respostas sem expor o número completo.</summary>
public static class CpfMasker
{
    /// <summary>Retorna apenas os dois dígitos finais do CPF.</summary>
    public static string Mask(string cpf) => cpf.Length == 11
        ? $"***.***.***-{cpf[^2..]}"
        : "CPF inválido";
}
