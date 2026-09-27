using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Domain.Banks;

/// <summary>Instituição financeira participante das transferências.</summary>
public sealed class Bank
{
    private Bank()
    {
    }

    private Bank(Guid id, string name, string ispb, string? compeCode)
    {
        Id = id;
        Name = name;
        Ispb = ispb;
        CompeCode = compeCode;
    }

    /// <summary>Identificador único da instituição.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome conhecido da instituição financeira.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador de oito dígitos usado no arranjo Pix.</summary>
    public string Ispb { get; private set; } = string.Empty;

    /// <summary>Código COMPE de três dígitos, quando disponível.</summary>
    public string? CompeCode { get; private set; }

    /// <summary>Cria uma instituição financeira após validar seus identificadores.</summary>
    /// <param name="name">Nome da instituição.</param>
    /// <param name="ispb">ISPB de oito dígitos.</param>
    /// <param name="compeCode">Código COMPE de três dígitos, se houver.</param>
    public static Result<Bank> Create(string? name, string? ispb, string? compeCode = null) =>
        Create(Guid.CreateVersion7(), name, ispb, compeCode);

    internal static Result<Bank> CreateSeeded(Guid id, string? name, string? ispb, string? compeCode = null) =>
        Create(id, name, ispb, compeCode);

    private static Result<Bank> Create(Guid id, string? name, string? ispb, string? compeCode)
    {
        var normalizedName = name?.Trim();
        var normalizedIspb = ispb?.Trim();
        var normalizedCompeCode = string.IsNullOrWhiteSpace(compeCode) ? null : compeCode.Trim();

        if (id == Guid.Empty || string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 120)
        {
            return Result<Bank>.Failure(new Error("bank.invalid_name", "O nome do banco deve conter de 1 a 120 caracteres."));
        }

        if (!HasDigits(normalizedIspb, 8))
        {
            return Result<Bank>.Failure(new Error("bank.invalid_ispb", "O ISPB deve conter exatamente 8 dígitos."));
        }

        if (normalizedCompeCode is not null && !HasDigits(normalizedCompeCode, 3))
        {
            return Result<Bank>.Failure(new Error("bank.invalid_compe_code", "O código COMPE deve conter exatamente 3 dígitos."));
        }

        return Result<Bank>.Success(new Bank(id, normalizedName, normalizedIspb!, normalizedCompeCode));
    }

    private static bool HasDigits(string? value, int length) =>
        value is not null && value.Length == length && value.All(char.IsAsciiDigit);
}
