using TesteTecnico.Api.Common.Results;

namespace TesteTecnico.Api.Domain.Accounts;

/// <summary>Conta usada para enviar e receber transferências bancárias.</summary>
public sealed class Account
{
    private const decimal MaximumMoney = 9_999_999_999_999_999.99m;
    private readonly List<PixKey> _pixKeys = [];

    private Account()
    {
    }

    private Account(
        Guid id,
        Guid ownerId,
        Guid bankId,
        BankAccountType type,
        string branch,
        string number,
        string? checkDigit,
        decimal overdraftLimit)
    {
        Id = id;
        OwnerId = ownerId;
        BankId = bankId;
        Type = type;
        Branch = branch;
        Number = number;
        CheckDigit = checkDigit;
        OverdraftLimit = overdraftLimit;
        Status = AccountStatus.Active;
    }

    /// <summary>Identificador único da conta no sistema.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador do titular único desta conta; um titular não pode possuir outra conta.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Identificador do banco cadastrado no sistema.</summary>
    public Guid BankId { get; private set; }

    /// <summary>Tipo da conta no banco.</summary>
    public BankAccountType Type { get; private set; }

    /// <summary>Agência bancária, sem dígito verificador.</summary>
    public string Branch { get; private set; } = string.Empty;

    /// <summary>Número da conta, sem dígito verificador.</summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>Dígito verificador da conta, quando existente.</summary>
    public string? CheckDigit { get; private set; }

    /// <summary>Saldo atual em reais; pode ser negativo até o limite de cheque especial.</summary>
    public decimal Balance { get; private set; }

    /// <summary>Valor máximo de saldo negativo permitido.</summary>
    public decimal OverdraftLimit { get; private set; }

    /// <summary>Situação operacional da conta.</summary>
    public AccountStatus Status { get; private set; }

    /// <summary>Chaves Pix registradas para esta conta.</summary>
    public IReadOnlyCollection<PixKey> PixKeys => _pixKeys;

    /// <summary>Abre uma conta ativa com saldo inicial zerado.</summary>
    /// <param name="ownerId">Identificador do titular.</param>
    /// <param name="bankId">Identificador de um banco cadastrado.</param>
    /// <param name="type">Tipo da conta bancária.</param>
    /// <param name="branch">Agência, contendo de 1 a 8 dígitos.</param>
    /// <param name="number">Número da conta, contendo de 1 a 20 dígitos.</param>
    /// <param name="checkDigit">Dígito da conta, quando existente.</param>
    /// <param name="overdraftLimit">Limite de cheque especial inicial.</param>
    public static Result<Account> Open(
        Guid ownerId,
        Guid bankId,
        BankAccountType type,
        string? branch,
        string? number,
        string? checkDigit,
        decimal overdraftLimit = 0m) =>
        Open(Guid.CreateVersion7(), ownerId, bankId, type, branch, number, checkDigit, overdraftLimit);

    internal static Result<Account> OpenSeeded(
        Guid id,
        Guid ownerId,
        Guid bankId,
        BankAccountType type,
        string? branch,
        string? number,
        string? checkDigit,
        decimal overdraftLimit) =>
        Open(id, ownerId, bankId, type, branch, number, checkDigit, overdraftLimit);

    private static Result<Account> Open(
        Guid id,
        Guid ownerId,
        Guid bankId,
        BankAccountType type,
        string? branch,
        string? number,
        string? checkDigit,
        decimal overdraftLimit)
    {
        var normalizedBranch = branch?.Trim();
        var normalizedNumber = number?.Trim();
        var normalizedCheckDigit = string.IsNullOrWhiteSpace(checkDigit) ? null : checkDigit.Trim().ToUpperInvariant();

        if (id == Guid.Empty || ownerId == Guid.Empty || bankId == Guid.Empty)
        {
            return InvalidAccount("A conta precisa ter um titular e um banco válidos.");
        }

        if (!Enum.IsDefined(type))
        {
            return InvalidAccount("O tipo da conta informado é inválido.");
        }

        if (!IsDigitsInRange(normalizedBranch, 1, 8) || !IsDigitsInRange(normalizedNumber, 1, 20))
        {
            return InvalidAccount("Agência e número da conta devem conter apenas dígitos e respeitar os tamanhos permitidos.");
        }

        if (normalizedCheckDigit is not null &&
            (normalizedCheckDigit.Length != 1 || !(char.IsAsciiDigit(normalizedCheckDigit[0]) || normalizedCheckDigit == "X")))
        {
            return InvalidAccount("O dígito verificador deve ser um dígito ou a letra X.");
        }

        if (overdraftLimit < 0m || overdraftLimit > MaximumMoney || decimal.Round(overdraftLimit, 2) != overdraftLimit)
        {
            return InvalidAccount("O limite de cheque especial deve caber em numeric(18,2) e ter no máximo duas casas decimais.");
        }

        return Result<Account>.Success(new Account(
            id,
            ownerId,
            bankId,
            type,
            normalizedBranch!,
            normalizedNumber!,
            normalizedCheckDigit,
            overdraftLimit));
    }

    /// <summary>Associa uma chave Pix à conta, evitando duplicatas locais.</summary>
    /// <param name="key">Chave Pix previamente validada.</param>
    public Result<Unit> AddPixKey(PixKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_pixKeys.Contains(key))
        {
            return ResultExtensions.Failure(new Error("account.duplicate_pix_key", "Esta chave Pix já está associada à conta."));
        }

        _pixKeys.Add(key);
        return ResultExtensions.Success();
    }

    /// <summary>Define o limite de cheque especial da conta.</summary>
    /// <param name="limit">Novo limite não negativo, com no máximo duas casas decimais.</param>
    public Result<Unit> SetOverdraftLimit(decimal limit)
    {
        if (limit < 0m || limit > MaximumMoney || decimal.Round(limit, 2) != limit)
        {
            return ResultExtensions.Failure(new Error("account.invalid_overdraft_limit", "O limite de cheque especial deve caber em numeric(18,2) e ter no máximo duas casas decimais."));
        }

        if (Balance < -limit)
        {
            return ResultExtensions.Failure(new Error("account.overdraft_below_balance", "O novo limite não pode ser menor que o cheque especial já utilizado."));
        }

        OverdraftLimit = limit;
        return ResultExtensions.Success();
    }

    /// <summary>Debita um valor respeitando a situação e o limite da conta.</summary>
    /// <param name="amount">Valor positivo a debitar, com no máximo duas casas decimais.</param>
    public Result<Unit> Debit(decimal amount)
    {
        var validation = ValidateDebit(amount);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        Balance -= amount;
        return ResultExtensions.Success();
    }

    /// <summary>Credita um valor à conta, desde que esteja ativa.</summary>
    /// <param name="amount">Valor positivo a creditar, com no máximo duas casas decimais.</param>
    public Result<Unit> Credit(decimal amount)
    {
        var validation = ValidateCredit(amount);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        Balance += amount;
        return ResultExtensions.Success();
    }

    /// <summary>Valida um débito sem alterar o saldo.</summary>
    public Result<Unit> ValidateDebit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return ResultExtensions.Failure(new Error("account.not_active", "A conta de origem não está ativa."));
        }

        if (!IsMoneyAmount(amount))
        {
            return ResultExtensions.Failure(new Error("account.invalid_amount", "O valor deve ser positivo e ter no máximo duas casas decimais."));
        }

        return Balance - amount < -OverdraftLimit
            ? ResultExtensions.Failure(new Error("account.insufficient_funds", "O saldo disponível e o cheque especial são insuficientes."))
            : ResultExtensions.Success();
    }

    /// <summary>Valida um crédito sem alterar o saldo.</summary>
    public Result<Unit> ValidateCredit(decimal amount)
    {
        if (Status != AccountStatus.Active)
        {
            return ResultExtensions.Failure(new Error("account.not_active", "A conta de destino não está ativa."));
        }

        if (!IsMoneyAmount(amount))
        {
            return ResultExtensions.Failure(new Error("account.invalid_amount", "O valor deve ser positivo e ter no máximo duas casas decimais."));
        }

        return Balance + amount > MaximumMoney
            ? ResultExtensions.Failure(new Error("account.balance_limit_exceeded", "O saldo excede o valor máximo suportado."))
            : ResultExtensions.Success();
    }

    /// <summary>Bloqueia a conta, impedindo débitos e créditos.</summary>
    public void Block() => Status = AccountStatus.Blocked;

    /// <summary>Ativa uma conta bloqueada ou inativa.</summary>
    public void Activate() => Status = AccountStatus.Active;

    /// <summary>Inativa a conta, impedindo débitos e créditos.</summary>
    public void Deactivate() => Status = AccountStatus.Inactive;

    private static Result<Account> InvalidAccount(string message) =>
        Result<Account>.Failure(new Error("account.invalid", message));

    private static bool IsDigitsInRange(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum && value.All(char.IsAsciiDigit);

    private static bool IsMoneyAmount(decimal amount) =>
        amount > 0m && amount <= MaximumMoney && decimal.Round(amount, 2) == amount;
}
