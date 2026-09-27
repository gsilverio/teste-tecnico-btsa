using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TesteTecnico.Api.Domain.Transfers;
using TesteTecnico.Api.Domain.TransferLimits;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Features.Transfers.Shared;

/// <summary>Avalia teto monetário e quantidade de tentativas da conta na janela configurada.</summary>
public sealed class TransferLimitEvaluator(
    AppDbContext dbContext,
    IOptions<TransferRulesOptions> rulesOptions)
{
    /// <summary>Retorna código de rejeição ou nulo quando a tentativa respeita a política.</summary>
    public async Task<string?> EvaluateAsync(
        TransferLimitPolicy? policy,
        Guid transferId,
        Guid sourceAccountId,
        decimal candidateAmount,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        if (policy is null)
        {
            return "transfer.limit_policy_missing";
        }

        var settings = rulesOptions.Value;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(attemptedAt, timeZone).DateTime);
        var isDayPeriod = localTime >= settings.DayStart && localTime < settings.NightStart;
        var maximumAmount = isDayPeriod ? policy.DayMaximumAmount : policy.NightMaximumAmount;
        var maximumAttempts = isDayPeriod ? policy.DayMaximumAttempts : policy.NightMaximumAttempts;
        var windowStart = attemptedAt.AddMinutes(-settings.WindowMinutes);

        var completedAmount = await dbContext.Transfers.AsNoTracking()
            .Where(transfer => transfer.SourceAccountId == sourceAccountId
                && transfer.Status == TransferStatus.Completed
                && transfer.FinishedAt >= windowStart)
            .SumAsync(transfer => (decimal?)transfer.Amount, cancellationToken) ?? 0m;

        var attemptsInWindow = await dbContext.TransferAttempts.AsNoTracking()
            .CountAsync(attempt => attempt.SourceAccountId == sourceAccountId
                && attempt.TransferId != transferId
                && attempt.AttemptedAt >= windowStart,
                cancellationToken);

        if (attemptsInWindow + 1 > maximumAttempts)
        {
            return "transfer.attempt_limit_exceeded";
        }

        return completedAmount + candidateAmount > maximumAmount
            ? "transfer.amount_limit_exceeded"
            : null;
    }
}
