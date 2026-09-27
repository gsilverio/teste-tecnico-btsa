using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Pagination;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.TransferLimits.GetPolicies;

/// <summary>Consulta políticas de limite de forma paginada.</summary>
public sealed class GetTransferLimitPoliciesQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna a página solicitada com o nome de cada titular.</summary>
    public async Task<Result<Page<TransferLimitPolicyResponse>>> HandleAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.TransferLimitPolicies.CountAsync(cancellationToken);
        var policies = await dbContext.TransferLimitPolicies.AsNoTracking()
            .OrderBy(policy => policy.AccountId)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var accountIds = policies.Select(policy => policy.AccountId).Distinct().ToArray();
        var holderNames = await (
                from account in dbContext.Accounts.AsNoTracking()
                join holder in dbContext.AccountHolders.AsNoTracking() on account.OwnerId equals holder.Id
                where accountIds.Contains(account.Id)
                select new { account.Id, holder.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var items = policies.Select(policy => new TransferLimitPolicyResponse(
            policy.Id,
            policy.AccountId,
            holderNames[policy.AccountId],
            policy.DayMaximumAmount,
            policy.DayMaximumAttempts,
            policy.NightMaximumAmount,
            policy.NightMaximumAttempts)).ToArray();

        return Result<Page<TransferLimitPolicyResponse>>.Success(
            Page<TransferLimitPolicyResponse>.Create(items, pagination.Page, pagination.PageSize, totalCount));
    }
}
