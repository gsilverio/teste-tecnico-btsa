using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.TransferLimits.DeletePolicy;

/// <summary>Remove uma política de limite de uma conta.</summary>
public sealed class DeleteTransferLimitPolicyCommandHandler(
    AppDbContext dbContext,
    ILogger<DeleteTransferLimitPolicyCommandHandler> logger)
{
    /// <summary>Exclui a configuração sem afetar a conta ou o titular.</summary>
    public async Task<Result<Unit>> HandleAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await dbContext.TransferLimitPolicies.SingleOrDefaultAsync(
            policy => policy.Id == policyId,
            cancellationToken);
        if (policy is null)
        {
            return Result<Unit>.Failure(new Error(
                "transfer_limit_policy.not_found",
                "A política de limite informada não foi encontrada."));
        }

        dbContext.TransferLimitPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Deleted transfer limit policy {PolicyId} for account {AccountId}",
            policy.Id,
            policy.AccountId);
        return ResultExtensions.Success();
    }
}
