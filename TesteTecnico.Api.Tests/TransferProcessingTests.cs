using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Domain.AccountHolders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Banks;
using TesteTecnico.Api.Domain.TransferLimits;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Features.Transfers.CancelTransfer;
using TesteTecnico.Api.Features.Transfers.ExecuteTransfer;
using TesteTecnico.Api.Features.Transfers.RequestTransfer;
using TesteTecnico.Api.Features.Transfers.Shared;
using TesteTecnico.Api.Features.Accounts.ManageStatus;
using TesteTecnico.Api.Features.Accounts.ManageOverdraft;
using TesteTecnico.Api.Infrastructure.Persistence;
using Xunit;

namespace TesteTecnico.Api.Tests;

/// <summary>Valida regras financeiras e concorrência de execução com PostgreSQL real.</summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class TransferProcessingTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Daytime = new(2026, 9, 26, 15, 0, 0, TimeSpan.Zero);

    [PostgreSqlFact]
    public async Task ImmediateRequest_WaitsForSharedExecutorAndReturnsFinalStateWithoutQueueDispatch()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 100m);
        await using var dbContext = fixture.CreateDbContext();
        var executor = CreateExecutor(dbContext, Daytime);
        var handler = new RequestTransferCommandHandler(
            dbContext,
            new UnexpectedBackgroundJobClient(),
            executor,
            new FixedTimeProvider(Daytime),
            NullLogger<RequestTransferCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new TransferRequest(
                accounts.Source,
                "BankAccount",
                125m,
                BankIspb: "12345678",
                Branch: "0001",
                AccountNumber: "00000002",
                CheckDigit: "0"),
            "immediate-transfer-test",
            scheduled: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value.Status);
        Assert.NotNull(result.Value.FinishedAt);
        Assert.Equal(375m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(225m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
        Assert.Equal(0, await dbContext.TransferOutboxMessages.CountAsync());
        var auditActions = await dbContext.AuditEntries
            .Where(entry => entry.Source == AuditSource.DomainEvent)
            .Where(entry => entry.TransferId == result.Value.Id)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => entry.ActionType)
            .ToArrayAsync();
        Assert.Equal(new[] { AuditActionType.TransferRequested, AuditActionType.TransferCompleted }, auditActions);
    }

    [PostgreSqlFact]
    public async Task TransferWithinOverdraft_CompletesAndUpdatesBothBalancesAtomically()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 1_000m, overdraftLimit: 1_000m);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 1_000m, Daytime);

        await ProcessAsync(transferId, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        var source = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source);
        var destination = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination);
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(-500m, source.Balance);
        Assert.Equal(2_000m, destination.Balance);
        Assert.Equal(TransferStatus.Completed, transfer.Status);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlTheory]
    [InlineData(15, 1000, "Completed", -500, 2000)]
    [InlineData(1, 1000, "Failed", 1500, 0)]
    [InlineData(1, 2000, "Completed", -500, 2000)]
    public async Task ExactOverdraftBoundary_RespectsHourlyPolicy(
        int utcHour, int nightLimit, string expectedStatus, int sourceBalance, int destinationBalance)
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(1500m, 0m, overdraftLimit: 500m, nightMaximumAmount: nightLimit);
        await using var dbContext = fixture.CreateDbContext();
        var instant = new DateTimeOffset(2026, 9, 28, utcHour, 12, 0, TimeSpan.Zero);
        var result = await CreateRequestHandler(dbContext, instant).HandleAsync(
            new TransferRequest(accounts.Source, "BankAccount", 2000m,
                BankIspb: "12345678", Branch: "0001", AccountNumber: "00000002", CheckDigit: "0"),
            "exact-overdraft-boundary", false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value.Status);
        Assert.Equal(expectedStatus == "Failed" ? "transfer.amount_limit_exceeded" : null, result.Value.FailureCode);
        Assert.Equal((decimal)sourceBalance, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal((decimal)destinationBalance, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task InsufficientFunds_FailsWithoutPartialMovementAndStillRecordsAttempt()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m, overdraftLimit: 100m);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 700m, Daytime);

        await ProcessAsync(transferId, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(500m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(0m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("account.insufficient_funds", transfer.FailureCode);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
        var audit = await dbContext.AuditEntries
            .Where(entry => entry.Source == AuditSource.DomainEvent)
            .Where(entry => entry.TransferId == transferId)
            .OrderBy(entry => entry.Sequence)
            .ToArrayAsync();
        Assert.Equal(new[] { AuditActionType.TransferRequested, AuditActionType.TransferFailed }, audit.Select(entry => entry.ActionType));
        Assert.Equal("account.insufficient_funds", audit[^1].ErrorMessage);
    }

    [PostgreSqlTheory]
    [InlineData(AccountStatus.Blocked, AccountStatus.Active, "account.source_blocked")]
    [InlineData(AccountStatus.Inactive, AccountStatus.Active, "account.source_inactive")]
    [InlineData(AccountStatus.Active, AccountStatus.Blocked, "account.destination_blocked")]
    [InlineData(AccountStatus.Active, AccountStatus.Inactive, "account.destination_inactive")]
    public async Task BlockedOrInactiveAccountsCannotSendOrReceive(
        AccountStatus sourceStatus,
        AccountStatus destinationStatus,
        string expectedFailureCode)
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 100m, sourceStatus: sourceStatus, destinationStatus: destinationStatus);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 100m, Daytime);

        await ProcessAsync(transferId, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal(expectedFailureCode, transfer.FailureCode);
        Assert.Equal(500m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(100m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
        var auditActions = await dbContext.AuditEntries
            .Where(entry => entry.Source == AuditSource.DomainEvent)
            .Where(entry => entry.TransferId == transferId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => entry.ActionType)
            .ToArrayAsync();
        Assert.Equal(new[] { AuditActionType.TransferRequested, AuditActionType.TransferFailed }, auditActions);
    }

    [PostgreSqlTheory]
    [InlineData(AccountStatus.Blocked, AccountStatus.Active, "account.source_blocked")]
    [InlineData(AccountStatus.Inactive, AccountStatus.Active, "account.source_inactive")]
    [InlineData(AccountStatus.Active, AccountStatus.Blocked, "account.destination_blocked")]
    [InlineData(AccountStatus.Active, AccountStatus.Inactive, "account.destination_inactive")]
    public async Task ScheduledTransferFailsOnceWhenSourceOrDestinationIsNotActiveAtExecution(
        AccountStatus sourceStatus,
        AccountStatus destinationStatus,
        string expectedFailureCode)
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 100m);
        var scheduledAt = Daytime.AddMinutes(1);
        var transferId = await CreateScheduledTransferAsync(accounts.Source, accounts.Destination, 100m, Daytime, scheduledAt);
        await SetAccountStatusAsync(accounts.Source, sourceStatus);
        await SetAccountStatusAsync(accounts.Destination, destinationStatus);

        await ProcessAsync(transferId, scheduledAt.AddSeconds(1));
        await ProcessAsync(transferId, scheduledAt.AddSeconds(2));

        await using var dbContext = fixture.CreateDbContext();
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal(expectedFailureCode, transfer.FailureCode);
        Assert.Equal(500m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(100m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
        var auditActions = await dbContext.AuditEntries
            .Where(entry => entry.Source == AuditSource.DomainEvent)
            .Where(entry => entry.TransferId == transferId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => entry.ActionType)
            .ToArrayAsync();
        Assert.Equal(new[] { AuditActionType.TransferScheduled, AuditActionType.TransferProcessingStarted, AuditActionType.TransferFailed }, auditActions);
    }

    [PostgreSqlFact]
    public async Task AccountHolderCannotOwnASecondAccountEvenAtAnotherBank()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 100m);
        await using var dbContext = fixture.CreateDbContext();
        var primary = await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.Source);
        var anotherBank = Bank.Create("Outro banco de teste", "87654321").Value;
        var anotherAccount = Account.Open(
            primary.OwnerId, anotherBank.Id, BankAccountType.Savings, "0001", "00000009", "0").Value;
        dbContext.AddRange(anotherBank, anotherAccount);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgres = Assert.IsType<Npgsql.PostgresException>(exception.InnerException);
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_accounts_OwnerId", postgres.ConstraintName);

        await using var verification = fixture.CreateDbContext();
        Assert.Equal(1, await verification.Accounts.CountAsync(account => account.OwnerId == primary.OwnerId));
        Assert.Equal(500m, (await verification.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(100m, (await verification.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
    }

    [PostgreSqlFact]
    public async Task AccountStatusCommandCanBlockAndReactivateAccount()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 100m);
        await using var dbContext = fixture.CreateDbContext();
        var handler = new ManageAccountStatusCommandHandler(dbContext, NullLogger<ManageAccountStatusCommandHandler>.Instance);

        var blocked = await handler.HandleAsync(accounts.Source, "Blocked", CancellationToken.None);
        var reactivated = await handler.HandleAsync(accounts.Source, "Active", CancellationToken.None);

        Assert.True(blocked.IsSuccess);
        Assert.Equal("Blocked", blocked.Value.Status);
        Assert.True(reactivated.IsSuccess);
        Assert.Equal("Active", reactivated.Value.Status);
    }

    [PostgreSqlFact]
    public async Task CreditToOverdrawnAccount_CoversUsedOverdraftBeforeCreatingPositiveBalance()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(1_000m, -800m, destinationOverdraftLimit: 1_000m);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 1_000m, Daytime);

        await ProcessAsync(transferId, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(200m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(0m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
    }

    [PostgreSqlFact]
    public async Task ExceedingAttemptLimit_FailsAndPersistsTheRejectedAttempt()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(1_000m, 0m, dayMaximumAttempts: 1);
        var firstId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 100m, Daytime);
        var secondDestination = await CreateAdditionalAccountAsync("00000003");
        var secondId = await CreateImmediateTransferAsync(accounts.Source, secondDestination, 100m, Daytime);

        await ProcessAsync(firstId, Daytime);
        await ProcessAsync(secondId, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        var rejected = await dbContext.Transfers.SingleAsync(item => item.Id == secondId);
        Assert.Equal(TransferStatus.Failed, rejected.Status);
        Assert.Equal("transfer.attempt_limit_exceeded", rejected.FailureCode);
        Assert.Equal(2, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task NightTransfer_UsesNightPolicy()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(1_000m, 0m, nightMaximumAmount: 50m);
        var night = new DateTimeOffset(2026, 9, 27, 2, 0, 0, TimeSpan.Zero); // 23:00 in São Paulo.
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 100m, night);

        await ProcessAsync(transferId, night);

        await using var dbContext = fixture.CreateDbContext();
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("transfer.amount_limit_exceeded", transfer.FailureCode);
    }

    [PostgreSqlFact]
    public async Task ScheduledTransfer_RevalidatesFundsWhenTheQueueExecutesIt()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(100m, 0m, overdraftLimit: 100m);
        var scheduledAt = Daytime.AddMinutes(1);
        var transferId = await CreateScheduledTransferAsync(accounts.Source, accounts.Destination, 300m, Daytime, scheduledAt);

        await ProcessAsync(transferId, scheduledAt.AddSeconds(1));

        await using var dbContext = fixture.CreateDbContext();
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("account.insufficient_funds", transfer.FailureCode);
        Assert.Equal(100m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentTransfersFromSameSource_DoNotOverspend()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(100m, 0m);
        var secondDestination = await CreateAdditionalAccountAsync("00000003");
        var firstId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 80m, Daytime);
        var secondId = await CreateImmediateTransferAsync(accounts.Source, secondDestination, 80m, Daytime);

        await Task.WhenAll(ProcessAsync(firstId, Daytime), ProcessAsync(secondId, Daytime));

        await using var dbContext = fixture.CreateDbContext();
        var source = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source);
        var transfers = await dbContext.Transfers.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(20m, source.Balance);
        Assert.Single(transfers, item => item.Status == TransferStatus.Completed);
        Assert.Single(transfers, item => item.Status == TransferStatus.Failed);
        Assert.Equal(2, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentImmediateRequests_LockAccountsBeforeInsertingAndDoNotOverspend()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(100m, 0m);
        var secondDestination = await CreateAdditionalAccountAsync("00000003");

        var results = await Task.WhenAll(
            RequestImmediateAsync(accounts.Source, "00000002", 80m, "concurrent-request-a"),
            RequestImmediateAsync(accounts.Source, "00000003", 80m, "concurrent-request-b"));

        await using var dbContext = fixture.CreateDbContext();
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(20m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Single(await dbContext.Transfers.Where(item => item.Status == TransferStatus.Completed).ToArrayAsync());
        Assert.Single(await dbContext.Transfers.Where(item => item.Status == TransferStatus.Failed).ToArrayAsync());
        Assert.Equal(2, await dbContext.TransferAttempts.CountAsync());
        Assert.NotEqual(accounts.Destination, secondDestination);
    }

    [PostgreSqlFact]
    public async Task ConcurrentRequestsWithSameKey_MoveMoneyOnlyOnce()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m);
        var results = await Task.WhenAll(
            RequestImmediateAsync(accounts.Source, "00000002", 100m, "same-key"),
            RequestImmediateAsync(accounts.Source, "00000002", 100m, "same-key"));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.Id, results[1].Value.Id);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Transfers.CountAsync());
        Assert.Equal(1, await db.TransferAttempts.CountAsync());
        Assert.Equal(400m, (await db.Accounts.SingleAsync(x => x.Id == accounts.Source)).Balance);
        Assert.Equal(100m, (await db.Accounts.SingleAsync(x => x.Id == accounts.Destination)).Balance);
    }

    [PostgreSqlFact]
    public async Task OppositeDirectionRequests_PreserveBalancesWithoutDeadlock()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 500m);
        await using (var setup = fixture.CreateDbContext())
        {
            setup.TransferLimitPolicies.Add(TransferLimitPolicy.Create(accounts.Destination, 5_000m, 5, 5_000m, 5).Value);
            await setup.SaveChangesAsync();
        }
        var results = await Task.WhenAll(
            RequestImmediateAsync(accounts.Source, "00000002", 100m, "forward"),
            RequestImmediateAsync(accounts.Destination, "00000001", 150m, "backward"));
        Assert.All(results, result => {
            Assert.True(result.IsSuccess);
            Assert.Equal("Completed", result.Value.Status);
        });
        await using var db = fixture.CreateDbContext();
        Assert.Equal(550m, (await db.Accounts.SingleAsync(x => x.Id == accounts.Source)).Balance);
        Assert.Equal(450m, (await db.Accounts.SingleAsync(x => x.Id == accounts.Destination)).Balance);
    }

    [PostgreSqlFact]
    public async Task RejectedPreflightRequest_CountsOnceWhenIdempotencyKeyIsReplayed()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m);
        await using var dbContext = fixture.CreateDbContext();
        var handler = CreateRequestHandler(dbContext, Daytime);
        var invalidRequest = new TransferRequest(
            accounts.Source,
            "BankAccount",
            0m,
            BankIspb: "12345678",
            Branch: "0001",
            AccountNumber: "00000002",
            CheckDigit: "0");

        var first = await handler.HandleAsync(invalidRequest, "invalid-amount-once", false, CancellationToken.None);
        var replay = await handler.HandleAsync(invalidRequest, "invalid-amount-once", false, CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.False(replay.IsSuccess);
        Assert.Equal("transfer.invalid_amount", first.Error!.Code);
        Assert.Equal(first.Error.Code, replay.Error!.Code);

        var sameAccount = await handler.HandleAsync(invalidRequest with
        {
            Amount = 50m,
            AccountNumber = "00000001"
        }, null, false, CancellationToken.None);
        var unknownDestination = await handler.HandleAsync(invalidRequest with
        {
            Amount = 50m,
            AccountNumber = "99999999"
        }, null, false, CancellationToken.None);

        Assert.False(sameAccount.IsSuccess);
        Assert.False(unknownDestination.IsSuccess);
        Assert.Equal(3, await dbContext.TransferAttempts.CountAsync());
        Assert.Equal(0, await dbContext.Transfers.CountAsync());
    }

    [PostgreSqlFact]
    public async Task TransferAmountLimit_SumsCompletedTransfersAndIgnoresExpiredWindowEntries()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(10_000m, 0m, dayMaximumAmount: 5_000m);
        var first = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 3_000m, Daytime.AddMinutes(-61));
        await ProcessAsync(first, Daytime.AddMinutes(-61));
        var second = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 3_000m, Daytime);
        var thirdDestination = await CreateAdditionalAccountAsync("00000003");
        var third = await CreateImmediateTransferAsync(accounts.Source, thirdDestination, 3_000m, Daytime);

        await ProcessAsync(second, Daytime);
        await ProcessAsync(third, Daytime);

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(TransferStatus.Completed, (await dbContext.Transfers.SingleAsync(item => item.Id == second)).Status);
        Assert.Equal(TransferStatus.Failed, (await dbContext.Transfers.SingleAsync(item => item.Id == third)).Status);
        Assert.Equal("transfer.amount_limit_exceeded", (await dbContext.Transfers.SingleAsync(item => item.Id == third)).FailureCode);
    }

    [PostgreSqlTheory]
    [InlineData(8, 59, TransferStatus.Failed)]
    [InlineData(9, 0, TransferStatus.Completed)]
    [InlineData(0, 59, TransferStatus.Completed)]
    [InlineData(1, 0, TransferStatus.Failed)]
    public async Task TransferLimit_UsesExpectedLocalDayAndNightBoundaries(int utcHour, int utcMinute, TransferStatus expected)
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(1_000m, 0m, nightMaximumAmount: 50m);
        var instant = new DateTimeOffset(2026, 9, 27, utcHour, utcMinute, 0, TimeSpan.Zero);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 100m, instant);

        await ProcessAsync(transferId, instant);

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(expected, (await dbContext.Transfers.SingleAsync(item => item.Id == transferId)).Status);
    }

    [PostgreSqlFact]
    public async Task ScheduledRequest_ReplayedAfterDueTimeReturnsExistingTransfer()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m);
        var scheduleAt = Daytime.AddMinutes(1);
        var request = new TransferRequest(
            accounts.Source,
            "BankAccount",
            100m,
            BankIspb: "12345678",
            Branch: "0001",
            AccountNumber: "00000002",
            CheckDigit: "0",
            ScheduledAt: scheduleAt);
        var jobClient = new RecordingBackgroundJobClient();

        await using (var firstContext = fixture.CreateDbContext())
        {
            var first = await CreateRequestHandler(firstContext, Daytime, jobClient)
                .HandleAsync(request, "scheduled-replay", true, CancellationToken.None);
            Assert.True(first.IsSuccess);
        }

        await using (var changedContext = fixture.CreateDbContext())
        {
            var changed = await CreateRequestHandler(changedContext, scheduleAt.AddMinutes(1), jobClient)
                .HandleAsync(request with { Amount = 100.001m }, "scheduled-replay", true, CancellationToken.None);

            Assert.False(changed.IsSuccess);
            Assert.Equal("transfer.idempotency_conflict", changed.Error!.Code);
            Assert.Equal(1, await changedContext.Transfers.CountAsync());
            Assert.Equal(0, await changedContext.TransferAttempts.CountAsync());
        }

        await using (var replayContext = fixture.CreateDbContext())
        {
            var replay = await CreateRequestHandler(replayContext, scheduleAt.AddMinutes(1), jobClient)
                .HandleAsync(request, "scheduled-replay", true, CancellationToken.None);

            Assert.True(replay.IsSuccess);
            Assert.Equal("Scheduled", replay.Value.Status);
            Assert.Equal(1, await replayContext.Transfers.CountAsync());
            Assert.Equal(1, await replayContext.TransferOutboxMessages.CountAsync());
        }
    }

    [PostgreSqlFact]
    public async Task OverdraftUpdate_RacingWithTransferPreservesBalanceConstraint()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(100m, 0m, overdraftLimit: 100m);

        var transfer = RequestImmediateAsync(accounts.Source, "00000002", 150m, "overdraft-race");
        var overdraftUpdate = SetOverdraftLimitAsync(accounts.Source, 0m);
        await Task.WhenAll(transfer, overdraftUpdate);

        await using var dbContext = fixture.CreateDbContext();
        var source = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source);
        Assert.True(source.Balance >= -source.OverdraftLimit);
    }

    [PostgreSqlFact]
    public async Task RedeliveryOfCompletedTransfer_DoesNotMoveBalancesTwice()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m);
        var transferId = await CreateImmediateTransferAsync(accounts.Source, accounts.Destination, 100m, Daytime);

        await ProcessAsync(transferId, Daytime);
        await ProcessAsync(transferId, Daytime.AddSeconds(5));

        await using var dbContext = fixture.CreateDbContext();
        Assert.Equal(400m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source)).Balance);
        Assert.Equal(100m, (await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination)).Balance);
        Assert.Equal(1, await dbContext.TransferAttempts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task CancellationRacingWithExecution_HasExactlyOneTerminalOutcome()
    {
        await fixture.ResetAsync();
        var accounts = await CreateAccountsAsync(500m, 0m);
        var scheduledAt = Daytime.AddMinutes(1);
        var transferId = await CreateScheduledTransferAsync(accounts.Source, accounts.Destination, 100m, Daytime, scheduledAt);

        var execution = ProcessAsync(transferId, scheduledAt.AddSeconds(1));
        var cancellation = CancelAsync(transferId, scheduledAt.AddSeconds(1));
        await Task.WhenAll(execution, cancellation);
        var cancellationResult = await cancellation;

        await using var dbContext = fixture.CreateDbContext();
        var transfer = await dbContext.Transfers.SingleAsync(item => item.Id == transferId);
        var source = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Source);
        var destination = await dbContext.Accounts.SingleAsync(account => account.Id == accounts.Destination);
        Assert.Contains(transfer.Status, new[] { TransferStatus.Completed, TransferStatus.Cancelled });
        if (transfer.Status == TransferStatus.Completed)
        {
            Assert.Equal(400m, source.Balance);
            Assert.Equal(100m, destination.Balance);
            Assert.False(cancellationResult.IsSuccess);
        }
        else
        {
            Assert.Equal(500m, source.Balance);
            Assert.Equal(0m, destination.Balance);
            Assert.True(cancellationResult.IsSuccess);
        }
    }

    private async Task<AccountSet> CreateAccountsAsync(
        decimal sourceBalance,
        decimal destinationBalance,
        decimal overdraftLimit = 0m,
        decimal dayMaximumAmount = 5_000m,
        int dayMaximumAttempts = 5,
        decimal nightMaximumAmount = 1_000m,
        decimal destinationOverdraftLimit = 0m,
        AccountStatus sourceStatus = AccountStatus.Active,
        AccountStatus destinationStatus = AccountStatus.Active)
    {
        var bank = Bank.Create("Banco de teste", "12345678").Value;
        var sourceHolder = AccountHolder.Create("Titular origem", "00000000191").Value;
        var destinationHolder = AccountHolder.Create("Titular destino", "00000000272").Value;
        var source = Account.Open(sourceHolder.Id, bank.Id, BankAccountType.Checking, "0001", "00000001", "0", overdraftLimit).Value;
        var destination = Account.Open(destinationHolder.Id, bank.Id, BankAccountType.Checking, "0001", "00000002", "0", destinationOverdraftLimit).Value;
        Assert.True(source.Credit(sourceBalance).IsSuccess);
        if (destinationBalance > 0m)
        {
            Assert.True(destination.Credit(destinationBalance).IsSuccess);
        }
        else if (destinationBalance < 0m)
        {
            Assert.True(destination.Debit(-destinationBalance).IsSuccess);
        }

        SetStatus(source, sourceStatus);
        SetStatus(destination, destinationStatus);

        var policy = TransferLimitPolicy.Create(source.Id, dayMaximumAmount, dayMaximumAttempts, nightMaximumAmount, 3).Value;
        await using var dbContext = fixture.CreateDbContext();
        dbContext.AddRange(bank, sourceHolder, destinationHolder, source, destination, policy);
        await dbContext.SaveChangesAsync();
        return new AccountSet(source.Id, destination.Id);
    }

    private async Task<Guid> CreateAdditionalAccountAsync(string number)
    {
        await using var dbContext = fixture.CreateDbContext();
        var bank = await dbContext.Banks.SingleAsync();
        var holder = AccountHolder.Create($"Titular {number}", "00000000353").Value;
        var account = Account.Open(holder.Id, bank.Id, BankAccountType.Checking, "0001", number, "0").Value;
        dbContext.AddRange(holder, account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    private async Task<Guid> CreateImmediateTransferAsync(Guid sourceId, Guid destinationId, decimal amount, DateTimeOffset createdAt)
    {
        await using var dbContext = fixture.CreateDbContext();
        var transfer = Transfer.CreateImmediate(sourceId, destinationId, amount, TransferMethod.Pix, createdAt).Value;
        dbContext.Transfers.Add(transfer);
        await dbContext.SaveChangesAsync();
        return transfer.Id;
    }

    private async Task<Guid> CreateScheduledTransferAsync(
        Guid sourceId,
        Guid destinationId,
        decimal amount,
        DateTimeOffset createdAt,
        DateTimeOffset scheduledAt)
    {
        await using var dbContext = fixture.CreateDbContext();
        var transfer = Transfer.Schedule(sourceId, destinationId, amount, TransferMethod.Pix, createdAt, scheduledAt).Value;
        dbContext.Transfers.Add(transfer);
        await dbContext.SaveChangesAsync();
        return transfer.Id;
    }

    private async Task ProcessAsync(Guid transferId, DateTimeOffset now)
    {
        await using var dbContext = fixture.CreateDbContext();
        await CreateExecutor(dbContext, now).HandleAsync(transferId, CancellationToken.None);
    }

    private async Task<Result<TransferResponse>> RequestImmediateAsync(
        Guid sourceId,
        string destinationNumber,
        decimal amount,
        string idempotencyKey)
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = CreateRequestHandler(dbContext, Daytime);
        return await handler.HandleAsync(new TransferRequest(
            sourceId,
            "BankAccount",
            amount,
            BankIspb: "12345678",
            Branch: "0001",
            AccountNumber: destinationNumber,
            CheckDigit: "0"), idempotencyKey, false, CancellationToken.None);
    }

    private static RequestTransferCommandHandler CreateRequestHandler(
        AppDbContext dbContext,
        DateTimeOffset now,
        IBackgroundJobClient? backgroundJobClient = null) =>
        new(
            dbContext,
            backgroundJobClient ?? new UnexpectedBackgroundJobClient(),
            CreateExecutor(dbContext, now),
            new FixedTimeProvider(now),
            NullLogger<RequestTransferCommandHandler>.Instance);

    private static ExecuteTransferCommandHandler CreateExecutor(AppDbContext dbContext, DateTimeOffset now)
    {
        var evaluator = new TransferLimitEvaluator(dbContext, Options.Create(new TransferRulesOptions()));
        return new ExecuteTransferCommandHandler(
            dbContext,
            evaluator,
            new FixedTimeProvider(now),
            NullLogger<ExecuteTransferCommandHandler>.Instance);
    }

    private static void SetStatus(Account account, AccountStatus status)
    {
        switch (status)
        {
            case AccountStatus.Blocked:
                account.Block();
                break;
            case AccountStatus.Inactive:
                account.Deactivate();
                break;
        }
    }

    private async Task<Result<TransferResponse>> CancelAsync(
        Guid transferId,
        DateTimeOffset now)
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new CancelTransferCommandHandler(dbContext, new FixedTimeProvider(now));
        return await handler.HandleAsync(transferId, CancellationToken.None);
    }

    private async Task SetAccountStatusAsync(Guid accountId, AccountStatus status)
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new ManageAccountStatusCommandHandler(dbContext, NullLogger<ManageAccountStatusCommandHandler>.Instance);
        var result = await handler.HandleAsync(accountId, status.ToString(), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private async Task<Result<OverdraftLimitResponse>> SetOverdraftLimitAsync(Guid accountId, decimal limit)
    {
        await using var dbContext = fixture.CreateDbContext();
        var handler = new SetOverdraftLimitCommandHandler(dbContext, NullLogger<SetOverdraftLimitCommandHandler>.Instance);
        return await handler.HandleAsync(accountId, limit, CancellationToken.None);
    }

    private sealed record AccountSet(Guid Source, Guid Destination);

    private sealed class UnexpectedBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => throw new InvalidOperationException("A transferência imediata não deve entrar no Hangfire.");

        public bool ChangeState(string jobId, IState state, string? expectedState) =>
            throw new InvalidOperationException("A transferência imediata não deve alterar um job do Hangfire.");
    }

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        private int nextId;

        public string Create(Job job, IState state) => Interlocked.Increment(ref nextId).ToString();

        public bool ChangeState(string jobId, IState state, string? expectedState) => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BTSA_TEST_CONNECTION")))
        {
            Skip = "Configure BTSA_TEST_CONNECTION para um banco PostgreSQL descartável terminado em _test.";
        }
    }
}

public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BTSA_TEST_CONNECTION")))
        {
            Skip = "Configure BTSA_TEST_CONNECTION para um banco PostgreSQL descartável terminado em _test.";
        }
    }
}
