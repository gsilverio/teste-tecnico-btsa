using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TesteTecnico.Api.Features.Transfers.ExecuteTransfer;
using TesteTecnico.Api.Features.Transfers.Shared;

namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Consome transferências no mesmo host HTTP e Hangfire da API.</summary>
public sealed class TransferQueueConsumer(
    RabbitMqConnection rabbitMqConnection,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TransferQueueConsumer> logger) : BackgroundService
{
    private const int MaximumProcessingRetries = 5;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await rabbitMqConnection.Connection.CreateChannelAsync(
            new CreateChannelOptions(true, true),
            stoppingToken);
        await DeclareQueuesAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            TransferQueueMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<TransferQueueMessage>(delivery.Body.Span);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Discarding malformed message from {QueueName}", TransferQueueNames.Processing);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                return;
            }

            if (message is null || message.TransferId == Guid.Empty)
            {
                logger.LogError("Discarding a message without a valid transfer ID from {QueueName}", TransferQueueNames.Processing);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                return;
            }

            var retryCount = ReadRetryCount(delivery.BasicProperties.Headers);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExecuteTransferCommandHandler>();
                await handler.HandleAsync(message.TransferId, stoppingToken);
                var dbContext = scope.ServiceProvider.GetRequiredService<TesteTecnico.Api.Infrastructure.Persistence.AppDbContext>();
                var outboxMessage = await dbContext.TransferOutboxMessages
                    .SingleOrDefaultAsync(item => item.TransferId == message.TransferId, stoppingToken);
                if (outboxMessage is not null)
                {
                    outboxMessage.MarkProcessingSucceeded();
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            }
            catch (TransferNotReadyException exception)
            {
                logger.LogWarning(exception, "Transfer {TransferId} arrived before its scheduled time; retrying later", message.TransferId);
                var delay = exception.ScheduledAt is { } scheduledAt
                    ? TimeSpan.FromSeconds(Math.Clamp((scheduledAt - timeProvider.GetUtcNow()).TotalSeconds + 1, 1, 60))
                    : TimeSpan.FromSeconds(2);
                await RequeueAfterDelayAsync(channel, delivery.DeliveryTag, delivery.Body, message.TransferId, retryCount, null, stoppingToken, delay);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Closing the channel leaves the unacknowledged message available for redelivery.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Transfer {TransferId} could not be processed; the message will be retried", message.TransferId);
                var diagnostic = $"Falha técnica ({exception.GetType().Name}). Consulte os logs pelo TransferId para detalhes.";
                await RequeueAfterDelayAsync(channel, delivery.DeliveryTag, delivery.Body, message.TransferId, retryCount, diagnostic, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(TransferQueueNames.Processing, false, consumer, stoppingToken);
        logger.LogInformation("Consuming transfer messages from RabbitMQ queue {QueueName}", TransferQueueNames.Processing);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task RequeueAfterDelayAsync(
        IChannel channel,
        ulong deliveryTag,
        ReadOnlyMemory<byte> body,
        Guid transferId,
        int retryCount,
        string? failureMessage,
        CancellationToken cancellationToken,
        TimeSpan? requestedDelay = null)
    {
        try
        {
            var deadLettered = failureMessage is not null && retryCount >= MaximumProcessingRetries;
            if (failureMessage is not null)
            {
                try
                {
                    await PersistProcessingFailureAsync(transferId, retryCount + 1, failureMessage, deadLettered, cancellationToken);
                    TransferMetrics.RecordQueueFailure(deadLettered);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Could not persist retry state for transfer {TransferId}; keeping the message in RabbitMQ", transferId);
                    await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, cancellationToken);
                    await channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
                    return;
                }
            }

            if (deadLettered)
            {
                logger.LogError("Transfer message exceeded {RetryCount} retries and will be moved to {DeadLetterQueue}", retryCount, TransferQueueNames.DeadLetter);
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            await Task.Delay(requestedDelay ?? TimeSpan.FromSeconds(2), timeProvider, cancellationToken);
            var retryProperties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    ["x-transfer-retries"] = retryCount + (failureMessage is null ? 0 : 1)
                }
            };
            try
            {
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: TransferQueueNames.Processing,
                    mandatory: true,
                    basicProperties: retryProperties,
                    body,
                    cancellationToken);
                await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
            }
            catch
            {
                await channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The unacknowledged delivery is returned to RabbitMQ when the channel closes.
        }
    }

    private async Task PersistProcessingFailureAsync(
        Guid transferId,
        int attempts,
        string failureMessage,
        bool deadLettered,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TesteTecnico.Api.Infrastructure.Persistence.AppDbContext>();
        var outboxMessage = await dbContext.TransferOutboxMessages
            .SingleOrDefaultAsync(item => item.TransferId == transferId, cancellationToken);
        if (outboxMessage is null)
        {
            return;
        }

        outboxMessage.RecordProcessingFailure(attempts, failureMessage, timeProvider.GetUtcNow(), deadLettered);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-transfer-retries", out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number is >= 0 and <= int.MaxValue => (int)number,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) => number,
            _ => 0
        };
    }

    private static async Task DeclareQueuesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(TransferQueueNames.DeadLetter, true, false, false, cancellationToken: cancellationToken);
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = TransferQueueNames.DeadLetter
        };
        await channel.QueueDeclareAsync(
            TransferQueueNames.Processing,
            true,
            false,
            false,
            arguments,
            cancellationToken: cancellationToken);
    }
}
