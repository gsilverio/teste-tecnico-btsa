using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.TransferLimits.UpdatePolicy;

/// <summary>Atualiza os tetos diurnos e noturnos de uma política existente.</summary>
public sealed class UpdateTransferLimitPolicyCommandHandler(
    AppDbContext dbContext,
    ILogger<UpdateTransferLimitPolicyCommandHandler> logger)
{
    /// <summary>Altera apenas os limites e tentativas, mantendo o titular associado.</summary>
    public async Task<Result<TransferLimitPolicyResponse>> HandleAsync(
        Guid policyId,
        TransferLimitPolicyValuesRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.TransferLimitPolicies.SingleOrDefaultAsync(
            policy => policy.Id == policyId,
            cancellationToken);
        if (policy is null)
        {
            return Result<TransferLimitPolicyResponse>.Failure(new Error(
                "transfer_limit_policy.not_found",
                "A política de limite informada não foi encontrada."));
        }

        var updateResult = policy.Update(
            request.DayMaximumAmount,
            request.DayMaximumAttempts,
            request.NightMaximumAmount,
            request.NightMaximumAttempts);
        if (!updateResult.IsSuccess)
        {
            return Result<TransferLimitPolicyResponse>.Failure(updateResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updated transfer limit policy {PolicyId} for account {AccountId}",
            policy.Id,
            policy.AccountId);
        var holderName = await (
                from account in dbContext.Accounts.AsNoTracking()
                join holder in dbContext.AccountHolders.AsNoTracking() on account.OwnerId equals holder.Id
                where account.Id == policy.AccountId
                select holder.Name)
            .SingleAsync(cancellationToken);

        return Result<TransferLimitPolicyResponse>.Success(new TransferLimitPolicyResponse(
            policy.Id,
            policy.AccountId,
            holderName,
            policy.DayMaximumAmount,
            policy.DayMaximumAttempts,
            policy.NightMaximumAmount,
            policy.NightMaximumAttempts));
    }
}
