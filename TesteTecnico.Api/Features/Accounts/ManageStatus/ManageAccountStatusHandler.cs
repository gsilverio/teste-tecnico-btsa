using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Accounts.ManageStatus;

/// <summary>Altera o estado operacional da conta protegendo a transição com o mesmo lock dos débitos/créditos.</summary>
public sealed class ManageAccountStatusCommandHandler(
    AppDbContext dbContext,
    ILogger<ManageAccountStatusCommandHandler> logger)
{
    /// <summary>Ativa, bloqueia ou inativa uma conta existente.</summary>
    public async Task<Result<AccountStatusResponse>> HandleAsync(
        Guid accountId,
        string status,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountStatus>(status, true, out var requestedStatus) || !Enum.IsDefined(requestedStatus))
        {
            return Result<AccountStatusResponse>.Failure(new Error(
                "account.invalid_status",
                "A situação deve ser Active, Blocked ou Inactive."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var account = await dbContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM \"accounts\" WHERE \"Id\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return Result<AccountStatusResponse>.Failure(new Error("account.not_found", "A conta informada não foi encontrada."));
        }

        switch (requestedStatus)
        {
            case AccountStatus.Active:
                account.Activate();
                break;
            case AccountStatus.Blocked:
                account.Block();
                break;
            case AccountStatus.Inactive:
                account.Deactivate();
                break;
            default:
                return Result<AccountStatusResponse>.Failure(new Error("account.invalid_status", "A situação informada é inválida."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Account {AccountId} status changed to {Status}", account.Id, requestedStatus);
        return Result<AccountStatusResponse>.Success(new AccountStatusResponse(account.Id, account.Status.ToString()));
    }
}

/// <summary>Representa a nova situação operacional da conta.</summary>
public sealed record AccountStatusResponse(Guid AccountId, string Status);

/// <summary>Define a situação desejada para uma conta.</summary>
public sealed record SetAccountStatusRequest(string Status);
