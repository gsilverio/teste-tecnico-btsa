using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Common;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Accounts.GetAccounts;

/// <summary>Consulta contas paginadas com titular, banco e chaves Pix para apoiar a operação local.</summary>
public sealed class GetAccountsQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna um conjunto limitado de contas e seus dados relacionados.</summary>
    public async Task<Result<Page<AccountSummaryResponse>>> HandleAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.Accounts.CountAsync(cancellationToken);
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.PixKeys)
            .OrderBy(account => account.Number)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var ownerIds = accounts.Select(account => account.OwnerId).Distinct().ToArray();
        var bankIds = accounts.Select(account => account.BankId).Distinct().ToArray();
        var holders = await dbContext.AccountHolders
            .AsNoTracking()
            .Where(holder => ownerIds.Contains(holder.Id))
            .ToDictionaryAsync(holder => holder.Id, cancellationToken);
        var banks = await dbContext.Banks
            .AsNoTracking()
            .Where(bank => bankIds.Contains(bank.Id))
            .ToDictionaryAsync(bank => bank.Id, cancellationToken);

        var items = accounts.Select(account => new AccountSummaryResponse(
            account.Id,
            account.OwnerId,
            holders[account.OwnerId].Name,
            CpfMasker.Mask(holders[account.OwnerId].Cpf),
            account.BankId,
            banks[account.BankId].Name,
            banks[account.BankId].Ispb,
            account.Type.ToString(),
            account.Branch,
            account.Number,
            account.CheckDigit,
            account.Balance,
            account.OverdraftLimit,
            account.Status.ToString(),
            account.PixKeys.Select(key => new PixKeyResponse(key.Type.ToString(), key.Value)).ToArray()))
            .ToArray();

        return Result<Page<AccountSummaryResponse>>.Success(
            Page<AccountSummaryResponse>.Create(items, pagination.Page, pagination.PageSize, totalCount));
    }
}

/// <summary>Conta com titular, banco e dados necessários para preparar uma transferência de teste.</summary>
public sealed record AccountSummaryResponse(
    Guid Id,
    Guid AccountHolderId,
    string AccountHolderName,
    string AccountHolderCpfMasked,
    Guid BankId,
    string BankName,
    string Ispb,
    string Type,
    string Branch,
    string Number,
    string? CheckDigit,
    decimal Balance,
    decimal OverdraftLimit,
    string Status,
    IReadOnlyList<PixKeyResponse> PixKeys);

/// <summary>Chave Pix exibida sem expor a entidade de domínio.</summary>
public sealed record PixKeyResponse(string Type, string Value);
