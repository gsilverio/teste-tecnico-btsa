using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Accounts.ManageOverdraft;

/// <summary>Lê o saldo e o limite de cheque especial de uma conta.</summary>
public sealed class GetOverdraftLimitQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna a configuração atual de cheque especial.</summary>
    public async Task<Result<OverdraftLimitResponse>> HandleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => new OverdraftLimitResponse(account.Id, account.Balance, account.OverdraftLimit))
            .SingleOrDefaultAsync(cancellationToken);

        return account is null
            ? Result<OverdraftLimitResponse>.Failure(new Error("account.not_found", "A conta informada não foi encontrada."))
            : Result<OverdraftLimitResponse>.Success(account);
    }
}

/// <summary>Altera o limite de cheque especial de uma conta existente.</summary>
public sealed class SetOverdraftLimitCommandHandler(
    AppDbContext dbContext,
    ILogger<SetOverdraftLimitCommandHandler> logger)
{
    /// <summary>Define o limite, validando que ele não fique abaixo do valor já utilizado.</summary>
    public async Task<Result<OverdraftLimitResponse>> HandleAsync(Guid accountId, decimal limit, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(account => account.Id == accountId, cancellationToken);
        if (account is null)
        {
            return Result<OverdraftLimitResponse>.Failure(new Error("account.not_found", "A conta informada não foi encontrada."));
        }

        var result = account.SetOverdraftLimit(limit);
        if (!result.IsSuccess)
        {
            return Result<OverdraftLimitResponse>.Failure(result.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated overdraft limit for account {AccountId}", account.Id);
        return Result<OverdraftLimitResponse>.Success(new OverdraftLimitResponse(account.Id, account.Balance, account.OverdraftLimit));
    }
}

/// <summary>Representa o saldo e a configuração de cheque especial de uma conta.</summary>
public sealed record OverdraftLimitResponse(Guid AccountId, decimal Balance, decimal OverdraftLimit);

/// <summary>Define o limite de cheque especial solicitado.</summary>
public sealed record SetOverdraftLimitRequest(decimal Limit);
