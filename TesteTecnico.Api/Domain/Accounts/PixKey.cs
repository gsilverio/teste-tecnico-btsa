using System.Net.Mail;
using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Domain.Accounts;

/// <summary>Chave Pix validada e associada a uma conta.</summary>
public sealed record PixKey
{
    private PixKey(PixKeyType type, string value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>Tipo da chave Pix.</summary>
    public PixKeyType Type { get; }

    /// <summary>Valor normalizado da chave Pix.</summary>
    public string Value { get; }

    /// <summary>Cria uma chave Pix após validar o tipo e a forma do valor.</summary>
    /// <param name="type">Tipo da chave Pix.</param>
    /// <param name="value">Valor informado para a chave.</param>
    public static Result<PixKey> Create(PixKeyType type, string? value)
    {
        if (!Enum.IsDefined(type) || string.IsNullOrWhiteSpace(value))
        {
            return InvalidKey();
        }

        var normalized = type switch
        {
            PixKeyType.Cpf or PixKeyType.Cnpj => new string(value.Where(char.IsAsciiDigit).ToArray()),
            PixKeyType.Phone => NormalizePhone(value),
            _ => value.Trim()
        };

        var valid = type switch
        {
            PixKeyType.Cpf => normalized.Length == 11,
            PixKeyType.Cnpj => normalized.Length == 14,
            PixKeyType.Email => IsEmail(normalized),
            PixKeyType.Phone => IsPhone(normalized),
            PixKeyType.Random => Guid.TryParse(normalized, out _),
            _ => false
        };

        return valid
            ? Result<PixKey>.Success(new PixKey(type, normalized))
            : InvalidKey();
    }

    private static Result<PixKey> InvalidKey() =>
        Result<PixKey>.Failure(new Error("account.invalid_pix_key", "A chave Pix não corresponde ao tipo informado."));

    private static bool IsEmail(string value)
    {
        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizePhone(string value)
    {
        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        return trimmed.StartsWith('+') ? $"+{digits}" : digits;
    }

    private static bool IsPhone(string value)
    {
        var digits = value.StartsWith('+') ? value[1..] : value;
        return digits.Length is >= 10 and <= 15 && digits.All(char.IsAsciiDigit);
    }
}
