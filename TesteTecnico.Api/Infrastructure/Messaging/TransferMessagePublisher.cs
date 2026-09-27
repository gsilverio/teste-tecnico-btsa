using System.Text.Json;
using RabbitMQ.Client;
using TesteTecnico.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Publica a outbox em uma fila RabbitMQ durável com confirmação do broker.</summary>
public sealed class TransferMessagePublisher(
    RabbitMqConnection rabbitMqConnection,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<TransferMessagePublisher> logger)
{
    /// <summary>Publica uma mensagem pendente e registra a confirmação após o ack do broker.</summary>
    public async Task PublishAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        var message = await dbContext.TransferOutboxMessages
            .SingleOrDefaultAsync(item => item.Id == outboxMessageId, cancellationToken);
        if (message is null || message.PublishedAt is not null)
        {
            logger.LogDebug("Outbox message {OutboxMessageId} is missing or already published", outboxMessageId);
            return;
        }

        if (message.AvailableAt > timeProvider.GetUtcNow())
        {
            logger.LogDebug("Outbox message {OutboxMessageId} is not due yet", outboxMessageId);
            return;
        }

        await using var channel = await rabbitMqConnection.Connection.CreateChannelAsync(
            new CreateChannelOptions(true, true),
            cancellationToken);
        await DeclareQueuesAsync(channel, cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(new TransferQueueMessage(message.TransferId));
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: TransferQueueNames.Processing,
            mandatory: true,
            basicProperties: properties,
            body,
            cancellationToken);

        message.MarkPublished(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Transfer {TransferId} published to RabbitMQ queue {QueueName} from outbox {OutboxMessageId}",
            message.TransferId,
            TransferQueueNames.Processing,
            message.Id);
    }

    private static async Task DeclareQueuesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: TransferQueueNames.DeadLetter,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = TransferQueueNames.DeadLetter
        };
        await channel.QueueDeclareAsync(
            queue: TransferQueueNames.Processing,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments,
            passive: false,
            noWait: false,
            cancellationToken);
    }
}
