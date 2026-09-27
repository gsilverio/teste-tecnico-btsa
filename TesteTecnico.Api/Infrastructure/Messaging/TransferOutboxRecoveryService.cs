using Microsoft.EntityFrameworkCore;
using TesteTecnico.Api.Infrastructure.Persistence;

namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Recupera mensagens da outbox se o processo cair entre o commit e o job Hangfire.</summary>
public sealed class TransferOutboxRecoveryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TransferOutboxRecoveryService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecoveryDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Transfer outbox recovery failed; it will retry after {Delay}", PollInterval);
            }

            await Task.Delay(PollInterval, timeProvider, stoppingToken);
        }
    }

    private async Task RecoverPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<TransferMessagePublisher>();
        var now = timeProvider.GetUtcNow();
        var recoveryCutoff = now - RecoveryDelay;
        var pendingIds = await dbContext.TransferOutboxMessages.AsNoTracking()
            .Where(message => message.PublishedAt == null
                && message.AvailableAt <= now
                && message.CreatedAt <= recoveryCutoff)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var outboxMessageId in pendingIds)
        {
            try
            {
                await publisher.PublishAsync(outboxMessageId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not recover outbox message {OutboxMessageId}", outboxMessageId);
            }
        }
    }
}
