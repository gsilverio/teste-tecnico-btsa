using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Features.Accounts.GetAccounts;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Accounts.GetAccount;

/// <summary>Consulta os dados de uma conta individual e os identifica junto ao titular e banco.</summary>
public sealed class GetAccountByIdQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna os dados correntes da conta, incluindo CPF e status operacional.</summary>
    public async Task<Result<AccountSummaryResponse>> HandleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking()
            .Include(item => item.PixKeys)
            .SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (account is null)
        {
            return Result<AccountSummaryResponse>.Failure(new Error("account.not_found", "A conta informada não foi encontrada."));
        }

        var holder = await dbContext.AccountHolders.AsNoTracking()
            .SingleAsync(item => item.Id == account.OwnerId, cancellationToken);
        var bank = await dbContext.Banks.AsNoTracking()
            .SingleAsync(item => item.Id == account.BankId, cancellationToken);
        return Result<AccountSummaryResponse>.Success(new AccountSummaryResponse(
            account.Id,
            holder.Id,
            holder.Name,
            CpfMasker.Mask(holder.Cpf),
            bank.Id,
            bank.Name,
            bank.Ispb,
            account.Type.ToString(),
            account.Branch,
            account.Number,
            account.CheckDigit,
            account.Balance,
            account.OverdraftLimit,
            account.Status.ToString(),
            account.PixKeys.Select(key => new PixKeyResponse(key.Type.ToString(), key.Value)).ToArray()));
    }
}
