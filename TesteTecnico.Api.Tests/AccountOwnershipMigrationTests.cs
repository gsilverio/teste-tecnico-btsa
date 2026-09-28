using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Infrastructure.Persistence;
using Xunit;

namespace TesteTecnico.Api.Tests;

/// <summary>Verifica a atualização do seed legado sem perder os dados financeiros.</summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class AccountOwnershipMigrationTests(PostgreSqlFixture fixture)
{
    [PostgreSqlFact]
    public async Task LegacySeedUpgrade_PreservesAccountsTransfersAndSettingsAndRemainsIdempotent()
    {
        await fixture.ResetAsync();
        await using var dbContext = fixture.CreateDbContext();
        await DevelopmentDataSeeder.SeedAsync(dbContext, NullLogger.Instance);
        var before = await dbContext.Accounts.AsNoTracking()
            .Select(account => new { account.Id, account.Balance, account.Status, account.OverdraftLimit })
            .OrderBy(account => account.Id).ToArrayAsync();
        var policyIds = await dbContext.TransferLimitPolicies.AsNoTracking()
            .OrderBy(policy => policy.Id).Select(policy => policy.Id).ToArrayAsync();
        var keys = await dbContext.Accounts.AsNoTracking().SelectMany(account => account.PixKeys)
            .OrderBy(key => key.Value).Select(key => key.Value).ToArrayAsync();
        var source = Guid.Parse("019942d0-0010-7000-8000-000000000010");
        var destination = Guid.Parse("019942d0-0022-7000-8000-000000000022");
        var now = DateTimeOffset.UtcNow;
        var transfer = Transfer.Schedule(source, destination, 50m, TransferMethod.Pix, now, now.AddDays(1)).Value;
        dbContext.Transfers.Add(transfer);
        await dbContext.SaveChangesAsync();

        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync("20260927191135_TrackRejectedAttemptsAndQueueFailures");
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                UPDATE accounts SET "OwnerId" = '019942d0-0100-7000-8000-000000000100'::uuid
                WHERE "Id" IN ('019942d0-0020-7000-8000-000000000020'::uuid,
                               '019942d0-0021-7000-8000-000000000021'::uuid,
                               '019942d0-0022-7000-8000-000000000022'::uuid);
                DELETE FROM account_holders WHERE "Id" IN (
                    '019942d0-0110-7000-8000-000000000110'::uuid,
                    '019942d0-0111-7000-8000-000000000111'::uuid,
                    '019942d0-0112-7000-8000-000000000112'::uuid);
                """);
        }
        finally
        {
            await migrator.MigrateAsync();
        }

        dbContext.ChangeTracker.Clear();
        await DevelopmentDataSeeder.SeedAsync(dbContext, NullLogger.Instance);
        Assert.Equal(13, await dbContext.AccountHolders.CountAsync());
        Assert.Equal(13, await dbContext.Accounts.Select(account => account.OwnerId).Distinct().CountAsync());
        Assert.Equal(before, await dbContext.Accounts.AsNoTracking()
            .Select(account => new { account.Id, account.Balance, account.Status, account.OverdraftLimit })
            .OrderBy(account => account.Id).ToArrayAsync());
        Assert.Equal(policyIds, await dbContext.TransferLimitPolicies.AsNoTracking()
            .OrderBy(policy => policy.Id).Select(policy => policy.Id).ToArrayAsync());
        Assert.Equal(keys, await dbContext.Accounts.AsNoTracking().SelectMany(account => account.PixKeys)
            .OrderBy(key => key.Value).Select(key => key.Value).ToArrayAsync());
        var preservedTransfer = await dbContext.Transfers.SingleAsync(item => item.Id == transfer.Id);
        Assert.Equal(source, preservedTransfer.SourceAccountId);
        Assert.Equal(destination, preservedTransfer.DestinationAccountId);
        Assert.Equal(TransferStatus.Scheduled, preservedTransfer.Status);
    }
}
