using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Domain.AccountHolders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Banks;
using TesteTecnico.Api.Domain.TransferLimits;

namespace TesteTecnico.Api.Infrastructure.Persistence;

/// <summary>Insere um conjunto fixo de dados para execução e validação locais.</summary>
public static class DevelopmentDataSeeder
{
    private static readonly SeedBank[] Banks =
    [
        new(Guid.Parse("019942d0-0001-7000-8000-000000000001"), "Banco Teste Azul", "99999991"),
        new(Guid.Parse("019942d0-0002-7000-8000-000000000002"), "Banco Teste Verde", "99999992"),
        new(Guid.Parse("019942d0-0003-7000-8000-000000000003"), "Banco Teste Laranja", "99999993")
    ];

    private static readonly SeedAccount[] Accounts =
    [
        new(Guid.Parse("019942d0-0010-7000-8000-000000000010"), Guid.Parse("019942d0-0100-7000-8000-000000000100"), Guid.Parse("019942d0-0200-7000-8000-000000000200"), "Titular de Teste 01", "00000000191", "99999991", "10000001", BankAccountType.Checking, 1_500m, 500m, 5_000m, 5, 1_000m, 3),
        new(Guid.Parse("019942d0-0011-7000-8000-000000000011"), Guid.Parse("019942d0-0101-7000-8000-000000000101"), Guid.Parse("019942d0-0201-7000-8000-000000000201"), "Titular de Teste 02", "00000000272", "99999992", "10000002", BankAccountType.Checking, 2_500m, 0m, 5_000m, 5, 1_000m, 3),
        new(Guid.Parse("019942d0-0012-7000-8000-000000000012"), Guid.Parse("019942d0-0102-7000-8000-000000000102"), Guid.Parse("019942d0-0202-7000-8000-000000000202"), "Titular de Teste 03", "00000000353", "99999993", "10000003", BankAccountType.Savings, 600m, 1_000m, 2_000m, 3, 500m, 2),
        new(Guid.Parse("019942d0-0013-7000-8000-000000000013"), Guid.Parse("019942d0-0103-7000-8000-000000000103"), Guid.Parse("019942d0-0203-7000-8000-000000000203"), "Titular de Teste 04", "00000000434", "99999991", "10000004", BankAccountType.Checking, 10_000m, 2_000m, 10_000m, 8, 2_000m, 5),
        new(Guid.Parse("019942d0-0014-7000-8000-000000000014"), Guid.Parse("019942d0-0104-7000-8000-000000000104"), Guid.Parse("019942d0-0204-7000-8000-000000000204"), "Titular de Teste 05", "00000000515", "99999992", "10000005", BankAccountType.Checking, 250m, 250m, 1_000m, 1, 250m, 1),
        new(Guid.Parse("019942d0-0015-7000-8000-000000000015"), Guid.Parse("019942d0-0105-7000-8000-000000000105"), Guid.Parse("019942d0-0205-7000-8000-000000000205"), "Titular de Teste 06", "00000000604", "99999993", "10000006", BankAccountType.Savings, 800m, 100m, 5_000m, 5, 1_000m, 3),
        new(Guid.Parse("019942d0-0016-7000-8000-000000000016"), Guid.Parse("019942d0-0106-7000-8000-000000000106"), Guid.Parse("019942d0-0206-7000-8000-000000000206"), "Titular de Teste 07", "00000000787", "99999991", "10000007", BankAccountType.Checking, 150m, 0m, 2_500m, 3, 500m, 1),
        new(Guid.Parse("019942d0-0017-7000-8000-000000000017"), Guid.Parse("019942d0-0107-7000-8000-000000000107"), Guid.Parse("019942d0-0207-7000-8000-000000000207"), "Titular de Teste 08", "00000000868", "99999992", "10000008", BankAccountType.Checking, 5_000m, 1_000m, 7_500m, 7, 1_500m, 4),
        new(Guid.Parse("019942d0-0018-7000-8000-000000000018"), Guid.Parse("019942d0-0108-7000-8000-000000000108"), Guid.Parse("019942d0-0208-7000-8000-000000000208"), "Titular de Teste 09", "00000000949", "99999993", "10000009", BankAccountType.Savings, 200m, 500m, 1_000m, 1, 250m, 1),
        new(Guid.Parse("019942d0-0019-7000-8000-000000000019"), Guid.Parse("019942d0-0109-7000-8000-000000000109"), Guid.Parse("019942d0-0209-7000-8000-000000000209"), "Titular de Teste 10", "00000001082", "99999991", "10000010", BankAccountType.Checking, 4_000m, 5_000m, 10_000m, 10, 2_500m, 5),
        new(Guid.Parse("019942d0-0020-7000-8000-000000000020"), Guid.Parse("019942d0-0100-7000-8000-000000000100"), Guid.Parse("019942d0-0220-7000-8000-000000000220"), "Titular de Teste 01", "00000000191", "99999992", "20000001", BankAccountType.Checking, 800m, 500m, 5_000m, 5, 1_000m, 3, AccountStatus.Blocked),
        new(Guid.Parse("019942d0-0021-7000-8000-000000000021"), Guid.Parse("019942d0-0100-7000-8000-000000000100"), Guid.Parse("019942d0-0221-7000-8000-000000000221"), "Titular de Teste 01", "00000000191", "99999993", "20000002", BankAccountType.Savings, 800m, 500m, 5_000m, 5, 1_000m, 3, AccountStatus.Inactive),
        new(Guid.Parse("019942d0-0022-7000-8000-000000000022"), Guid.Parse("019942d0-0100-7000-8000-000000000100"), Guid.Parse("019942d0-0222-7000-8000-000000000222"), "Titular de Teste 01", "00000000191", "99999992", "20000003", BankAccountType.Checking, 500m, 0m, 5_000m, 5, 1_000m, 3)
    ];

    /// <summary>Insere bancos, titulares, contas e políticas ausentes sem sobrescrever alterações locais.</summary>
    public static async Task SeedAsync(
        AppDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var addedBanks = 0;
        var addedHolders = 0;
        var addedAccounts = 0;
        var addedPolicies = 0;

        var banksByIspb = await dbContext.Banks.ToDictionaryAsync(bank => bank.Ispb, cancellationToken);
        foreach (var seedBank in Banks)
        {
            if (!banksByIspb.ContainsKey(seedBank.Ispb))
            {
                var bank = Bank.CreateSeeded(seedBank.Id, seedBank.Name, seedBank.Ispb);
                banksByIspb.Add(seedBank.Ispb, bank.Value);
                dbContext.Banks.Add(bank.Value);
                addedBanks++;
            }
        }

        var holdersById = await dbContext.AccountHolders.ToDictionaryAsync(holder => holder.Id, cancellationToken);
        foreach (var seedAccount in Accounts)
        {
            if (!holdersById.ContainsKey(seedAccount.HolderId))
            {
                var holder = AccountHolder.CreateSeeded(seedAccount.HolderId, seedAccount.HolderName, seedAccount.HolderCpf);
                holdersById.Add(seedAccount.HolderId, holder.Value);
                dbContext.AccountHolders.Add(holder.Value);
                addedHolders++;
            }
        }

        var existingAccounts = await dbContext.Accounts
            .Select(account => new { account.Id, account.BankId, account.Branch, account.Number })
            .ToListAsync(cancellationToken);
        var existingAccountKeys = existingAccounts
            .Select(account => (account.BankId, account.Branch, account.Number))
            .ToHashSet();
        var newlySeededAccountIds = new HashSet<Guid>();

        foreach (var seedAccount in Accounts)
        {
            var bank = banksByIspb[seedAccount.BankIspb];
            if (existingAccountKeys.Contains((bank.Id, "0001", seedAccount.Number)))
            {
                continue;
            }

            var holder = holdersById[seedAccount.HolderId];
            var account = Account.OpenSeeded(
                seedAccount.Id,
                holder.Id,
                bank.Id,
                seedAccount.Type,
                "0001",
                seedAccount.Number,
                "0",
                seedAccount.OverdraftLimit);
            if (!account.IsSuccess)
            {
                throw new InvalidOperationException($"Não foi possível criar a conta de demonstração {seedAccount.Number}: {account.Error!.Message}");
            }

            if (seedAccount.OpeningBalance > 0m && !account.Value.Credit(seedAccount.OpeningBalance).IsSuccess)
            {
                throw new InvalidOperationException($"Não foi possível definir o saldo de demonstração para a conta {seedAccount.Number}.");
            }

            var pixKey = PixKey.Create(PixKeyType.Email, $"conta{seedAccount.Number}@example.com");
            if (!pixKey.IsSuccess || !account.Value.AddPixKey(pixKey.Value).IsSuccess)
            {
                throw new InvalidOperationException($"Não foi possível adicionar a chave Pix à conta de demonstração {seedAccount.Number}.");
            }

            switch (seedAccount.Status)
            {
                case AccountStatus.Blocked:
                    account.Value.Block();
                    break;
                case AccountStatus.Inactive:
                    account.Value.Deactivate();
                    break;
            }

            dbContext.Accounts.Add(account.Value);
            addedAccounts++;
            newlySeededAccountIds.Add(account.Value.Id);
        }

        var existingPolicyAccountIds = await dbContext.TransferLimitPolicies
            .Select(policy => policy.AccountId)
            .ToListAsync(cancellationToken);
        var existingPolicyAccounts = existingPolicyAccountIds.ToHashSet();

        foreach (var seedAccount in Accounts)
        {
            if (existingPolicyAccounts.Contains(seedAccount.Id) || !newlySeededAccountIds.Contains(seedAccount.Id))
            {
                continue;
            }

            var policy = TransferLimitPolicy.CreateSeeded(
                seedAccount.PolicyId,
                seedAccount.Id,
                seedAccount.DayMaximumAmount,
                seedAccount.DayMaximumAttempts,
                seedAccount.NightMaximumAmount,
                seedAccount.NightMaximumAttempts);
            dbContext.TransferLimitPolicies.Add(policy.Value);
            addedPolicies++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (addedBanks + addedHolders + addedAccounts + addedPolicies > 0)
        {
            logger.LogInformation(
                "Development seed added {BankCount} banks, {HolderCount} account holders, {AccountCount} accounts and {PolicyCount} transfer limit policies",
                addedBanks,
                addedHolders,
                addedAccounts,
                addedPolicies);
        }
        else
        {
            logger.LogDebug("Development seed is already up to date");
        }
    }

    private sealed record SeedBank(Guid Id, string Name, string Ispb);

    private sealed record SeedAccount(
        Guid Id,
        Guid HolderId,
        Guid PolicyId,
        string HolderName,
        string HolderCpf,
        string BankIspb,
        string Number,
        BankAccountType Type,
        decimal OpeningBalance,
        decimal OverdraftLimit,
        decimal DayMaximumAmount,
        int DayMaximumAttempts,
        decimal NightMaximumAmount,
        int NightMaximumAttempts,
        AccountStatus Status = AccountStatus.Active);
}
