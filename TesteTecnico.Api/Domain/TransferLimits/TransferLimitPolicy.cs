using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Domain.TransferLimits;

/// <summary>Define os tetos de valor e de tentativas de transferência de uma conta.</summary>
public sealed class TransferLimitPolicy
{
    private const decimal MaximumMoney = 9_999_999_999_999_999.99m;

    private TransferLimitPolicy()
    {
    }

    private TransferLimitPolicy(
        Guid id,
        Guid accountId,
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts)
    {
        Id = id;
        AccountId = accountId;
        DayMaximumAmount = dayMaximumAmount;
        DayMaximumAttempts = dayMaximumAttempts;
        NightMaximumAmount = nightMaximumAmount;
        NightMaximumAttempts = nightMaximumAttempts;
    }

    /// <summary>Identificador da política.</summary>
    public Guid Id { get; private set; }

    /// <summary>Conta à qual a política pertence; cada conta tem no máximo uma política.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Valor máximo agregado no período diurno.</summary>
    public decimal DayMaximumAmount { get; private set; }

    /// <summary>Número máximo de tentativas no período diurno.</summary>
    public int DayMaximumAttempts { get; private set; }

    /// <summary>Valor máximo agregado no período noturno.</summary>
    public decimal NightMaximumAmount { get; private set; }

    /// <summary>Número máximo de tentativas no período noturno.</summary>
    public int NightMaximumAttempts { get; private set; }

    /// <summary>Cria uma política para uma conta após validar os valores e quantidades configurados.</summary>
    public static Result<TransferLimitPolicy> Create(
        Guid accountId,
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts) =>
        Create(Guid.CreateVersion7(), accountId, dayMaximumAmount, dayMaximumAttempts, nightMaximumAmount, nightMaximumAttempts);

    internal static Result<TransferLimitPolicy> CreateSeeded(
        Guid id,
        Guid accountId,
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts) =>
        Create(id, accountId, dayMaximumAmount, dayMaximumAttempts, nightMaximumAmount, nightMaximumAttempts);

    /// <summary>Altera os limites diurnos e noturnos desta política.</summary>
    public Result<Unit> Update(
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts)
    {
        var validation = Validate(AccountId, dayMaximumAmount, dayMaximumAttempts, nightMaximumAmount, nightMaximumAttempts);
        if (validation is not null)
        {
            return ResultExtensions.Failure(validation);
        }

        DayMaximumAmount = dayMaximumAmount;
        DayMaximumAttempts = dayMaximumAttempts;
        NightMaximumAmount = nightMaximumAmount;
        NightMaximumAttempts = nightMaximumAttempts;
        return ResultExtensions.Success();
    }

    private static Result<TransferLimitPolicy> Create(
        Guid id,
        Guid accountId,
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts)
    {
        if (id == Guid.Empty)
        {
            return Result<TransferLimitPolicy>.Failure(new Error("transfer_limit_policy.invalid_id", "O identificador da política é inválido."));
        }

        var validation = Validate(accountId, dayMaximumAmount, dayMaximumAttempts, nightMaximumAmount, nightMaximumAttempts);
        if (validation is not null)
        {
            return Result<TransferLimitPolicy>.Failure(validation);
        }

        return Result<TransferLimitPolicy>.Success(new TransferLimitPolicy(
            id,
            accountId,
            dayMaximumAmount,
            dayMaximumAttempts,
            nightMaximumAmount,
            nightMaximumAttempts));
    }

    private static Error? Validate(
        Guid accountId,
        decimal dayMaximumAmount,
        int dayMaximumAttempts,
        decimal nightMaximumAmount,
        int nightMaximumAttempts)
    {
        if (accountId == Guid.Empty)
        {
            return new Error("transfer_limit_policy.invalid_account", "A conta informada é inválida.");
        }

        if (!IsMoneyLimit(dayMaximumAmount) || !IsMoneyLimit(nightMaximumAmount))
        {
            return new Error("transfer_limit_policy.invalid_amount", "Os tetos monetários devem ser não negativos, caber em numeric(18,2) e ter no máximo duas casas decimais.");
        }

        if (dayMaximumAttempts < 0 || nightMaximumAttempts < 0)
        {
            return new Error("transfer_limit_policy.invalid_attempts", "A quantidade máxima de tentativas não pode ser negativa.");
        }

        return null;
    }

    private static bool IsMoneyLimit(decimal amount) =>
        amount >= 0m && amount <= MaximumMoney && decimal.Round(amount, 2) == amount;
}
