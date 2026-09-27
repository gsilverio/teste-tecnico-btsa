using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.TransferLimits.GetPolicy;

/// <summary>Consulta uma política de limite pela identidade persistida.</summary>
public sealed class GetTransferLimitPolicyQueryHandler(AppDbContext dbContext)
{
    /// <summary>Retorna a política da conta e o nome do titular ou uma falha de recurso ausente.</summary>
    public async Task<Result<TransferLimitPolicyResponse>> HandleAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await dbContext.TransferLimitPolicies.AsNoTracking()
            .Where(policy => policy.Id == policyId)
            .Select(policy => new
            {
                Policy = policy,
                HolderName = (
                    from account in dbContext.Accounts
                    join holder in dbContext.AccountHolders on account.OwnerId equals holder.Id
                    where account.Id == policy.AccountId
                    select holder.Name)
                    .Single()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return policy is null
            ? Result<TransferLimitPolicyResponse>.Failure(new Error(
                "transfer_limit_policy.not_found",
                "A política de limite informada não foi encontrada."))
            : Result<TransferLimitPolicyResponse>.Success(new TransferLimitPolicyResponse(
                policy.Policy.Id,
                policy.Policy.AccountId,
                policy.HolderName,
                policy.Policy.DayMaximumAmount,
                policy.Policy.DayMaximumAttempts,
                policy.Policy.NightMaximumAmount,
                policy.Policy.NightMaximumAttempts));
    }
}
