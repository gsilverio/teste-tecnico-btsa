using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Domain.AccountHolders;

/// <summary>Identifica a pessoa titular de uma conta neste sistema.</summary>
public sealed class AccountHolder
{
    private AccountHolder()
    {
    }

    private AccountHolder(Guid id, string name, string cpf)
    {
        Id = id;
        Name = name;
        Cpf = cpf;
    }

    /// <summary>Identificador único do titular.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome usado para identificar o titular nas consultas de conta e transferência.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>CPF normalizado, armazenado somente com os onze dígitos.</summary>
    public string Cpf { get; private set; } = string.Empty;

    /// <summary>Cria um titular identificado pelo nome e por um CPF válido.</summary>
    /// <param name="name">Nome do titular.</param>
    /// <param name="cpf">CPF com ou sem pontuação.</param>
    public static Result<AccountHolder> Create(string? name, string? cpf) => Create(Guid.CreateVersion7(), name, cpf);

    internal static Result<AccountHolder> CreateSeeded(Guid id, string? name, string? cpf) => Create(id, name, cpf);

    private static Result<AccountHolder> Create(Guid id, string? name, string? cpf)
    {
        var normalizedName = name?.Trim();
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 120)
        {
            return Result<AccountHolder>.Failure(new Error(
                "account_holder.invalid_name",
                "O nome do titular deve conter de 1 a 120 caracteres."));
        }

        var normalizedCpf = NormalizeCpf(cpf);
        if (normalizedCpf is null)
        {
            return Result<AccountHolder>.Failure(new Error(
                "account_holder.invalid_cpf",
                "Informe um CPF válido com onze dígitos."));
        }

        return Result<AccountHolder>.Success(new AccountHolder(id, normalizedName, normalizedCpf));
    }

    private static string? NormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Any(character =>
                !char.IsAsciiDigit(character) && character is not '.' and not '-' and not ' '))
        {
            return null;
        }

        var digits = new string(cpf.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 11 || digits.All(character => character == digits[0]))
        {
            return null;
        }

        var firstDigit = CalculateCheckDigit(digits, 9, 10);
        if (digits[9] - '0' != firstDigit)
        {
            return null;
        }

        var secondDigit = CalculateCheckDigit(digits, 10, 11);
        return digits[10] - '0' == secondDigit ? digits : null;
    }

    private static int CalculateCheckDigit(string digits, int length, int firstWeight)
    {
        var sum = 0;
        for (var index = 0; index < length; index++)
        {
            sum += (digits[index] - '0') * (firstWeight - index);
        }

        var remainder = sum * 10 % 11;
        return remainder == 10 ? 0 : remainder;
    }
}
