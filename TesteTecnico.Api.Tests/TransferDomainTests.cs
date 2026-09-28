using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;
using Xunit;

namespace TesteTecnico.Api.Tests;

public sealed class TransferDomainTests
{
    [Fact]
    public void ZeroBalanceAccount_CanReceiveTransferWithoutInitialMovement()
    {
        var account = Account.Open(Guid.NewGuid(), Guid.NewGuid(), BankAccountType.Checking, "0001", "1234", "0").Value;
        Assert.Equal(0m, account.Balance);
        Assert.True(account.Credit(1.10m).IsSuccess);
        Assert.Equal(1.10m, account.Balance);
    }

    [Theory]
    [InlineData("100.001")]
    [InlineData("0")]
    [InlineData("-1")]
    public void InvalidAmount_IsRejectedBeforeTemporalOrIdempotencyChecks(string amount)
    {
        var error = Transfer.ValidateRequest(Guid.NewGuid(), Guid.NewGuid(),
            decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), TransferMethod.Pix);
        Assert.Equal("transfer.invalid_amount", error?.Code);
    }

    [Theory]
    [InlineData("0.07")]
    [InlineData("0.29")]
    [InlineData("1.10")]
    [InlineData("2.30")]
    public void CentAmounts_AreAccepted(string amount)
    {
        Assert.Null(Transfer.ValidateRequest(Guid.NewGuid(), Guid.NewGuid(),
            decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), TransferMethod.Pix));
    }
}
