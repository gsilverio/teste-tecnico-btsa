using TesteTecnico.Api.Infrastructure.Messaging;
using Hangfire;

namespace TesteTecnico.Api.Infrastructure.Jobs;

/// <summary>Publica uma transferência persistida no horário escolhido pelo Hangfire.</summary>
[AutomaticRetry(Attempts = 5, DelaysInSeconds = [2, 5, 15, 30, 60])]
public sealed class TransferDispatchJob(
    TransferMessagePublisher publisher,
    ILogger<TransferDispatchJob> logger)
{
    /// <summary>Entrega o identificador da outbox à fila durável do RabbitMQ.</summary>
    public async Task DispatchAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Hangfire dispatch job started for outbox {OutboxMessageId}", outboxMessageId);
        await publisher.PublishAsync(outboxMessageId, cancellationToken);
    }
}
