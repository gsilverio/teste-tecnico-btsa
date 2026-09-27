using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Common.Results;
using TesteTecnico.Api.Domain.TransferLimits;
using TesteTecnico.Api.Features.TransferLimits.Shared;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.TransferLimits.CreatePolicy;

/// <summary>Cria uma única política de limite para uma conta existente.</summary>
public sealed class CreateTransferLimitPolicyCommandHandler(
    AppDbContext dbContext,
    ILogger<CreateTransferLimitPolicyCommandHandler> logger)
{
    /// <summary>Valida a relação com a conta, cria a política e persiste os limites informados.</summary>
    public async Task<Result<TransferLimitPolicyResponse>> HandleAsync(
        CreateTransferLimitPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var account = await (
                from targetAccount in dbContext.Accounts.AsNoTracking()
                join holder in dbContext.AccountHolders.AsNoTracking() on targetAccount.OwnerId equals holder.Id
                where targetAccount.Id == request.AccountId
                select new { targetAccount.Id, HolderName = holder.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return Result<TransferLimitPolicyResponse>.Failure(new Error(
                "account.not_found",
                "A conta informada não foi encontrada."));
        }

        if (await dbContext.TransferLimitPolicies.AnyAsync(
                policy => policy.AccountId == request.AccountId,
                cancellationToken))
        {
            return Result<TransferLimitPolicyResponse>.Failure(new Error(
                "transfer_limit_policy.already_exists",
                "A conta já possui uma política de limite."));
        }

        var policyResult = TransferLimitPolicy.Create(
            request.AccountId,
            request.DayMaximumAmount,
            request.DayMaximumAttempts,
            request.NightMaximumAmount,
            request.NightMaximumAttempts);
        if (!policyResult.IsSuccess)
        {
            return Result<TransferLimitPolicyResponse>.Failure(policyResult.Error!);
        }

        dbContext.TransferLimitPolicies.Add(policyResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created transfer limit policy {PolicyId} for account {AccountId}",
            policyResult.Value.Id,
            policyResult.Value.AccountId);
        return Result<TransferLimitPolicyResponse>.Success(ToResponse(policyResult.Value, account.HolderName));
    }

    private static TransferLimitPolicyResponse ToResponse(TransferLimitPolicy policy, string holderName) => new(
        policy.Id,
        policy.AccountId,
        holderName,
        policy.DayMaximumAmount,
        policy.DayMaximumAttempts,
        policy.NightMaximumAmount,
        policy.NightMaximumAttempts);
}
