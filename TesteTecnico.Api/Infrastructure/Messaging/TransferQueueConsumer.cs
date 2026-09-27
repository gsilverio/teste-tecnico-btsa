using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TesteTecnico.Api.Features.Transfers.ExecuteTransfer;

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
                await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            }
            catch (TransferNotReadyException exception)
            {
                logger.LogWarning(exception, "Transfer {TransferId} arrived before its scheduled time; retrying later", message.TransferId);
                await RequeueAfterDelayAsync(channel, delivery.DeliveryTag, delivery.Body, retryCount, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Closing the channel leaves the unacknowledged message available for redelivery.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Transfer {TransferId} could not be processed; the message will be retried", message.TransferId);
                await RequeueAfterDelayAsync(channel, delivery.DeliveryTag, delivery.Body, retryCount, stoppingToken);
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
        int retryCount,
        CancellationToken cancellationToken)
    {
        try
        {
            if (retryCount >= MaximumProcessingRetries)
            {
                logger.LogError("Transfer message exceeded {RetryCount} retries and will be moved to {DeadLetterQueue}", retryCount, TransferQueueNames.DeadLetter);
                await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, cancellationToken);
            var retryProperties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    ["x-transfer-retries"] = retryCount + 1
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
