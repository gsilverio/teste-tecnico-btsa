namespace TesteTecnico.Api.Infrastructure.Persistence;

/// <summary>Mensagem persistida junto à transferência até sua confirmação no RabbitMQ.</summary>
public sealed class TransferOutboxMessage
{
    private TransferOutboxMessage()
    {
    }

    private TransferOutboxMessage(Guid id, Guid transferId, DateTimeOffset availableAt, DateTimeOffset createdAt)
    {
        Id = id;
        TransferId = transferId;
        AvailableAt = availableAt.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
    }

    /// <summary>Identificador usado pelo job de despacho.</summary>
    public Guid Id { get; private set; }

    /// <summary>Transferência que será enviada para a fila.</summary>
    public Guid TransferId { get; private set; }

    /// <summary>Instante a partir do qual a mensagem pode ser publicada.</summary>
    public DateTimeOffset AvailableAt { get; private set; }

    /// <summary>Instante de criação do registro da outbox.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Instante em que o broker confirmou a publicação.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Quantidade de falhas técnicas de processamento da mensagem.</summary>
    public int ProcessingAttempts { get; private set; }

    /// <summary>Última falha técnica observada ao processar a transferência.</summary>
    public string? LastProcessingError { get; private set; }

    /// <summary>Instante em que a mensagem foi encaminhada à dead-letter queue.</summary>
    public DateTimeOffset? DeadLetteredAt { get; private set; }

    /// <summary>Marca a mensagem como publicada após confirmação do RabbitMQ.</summary>
    public void MarkPublished(DateTimeOffset publishedAt) => PublishedAt = publishedAt.ToUniversalTime();

    /// <summary>Registra falha de processamento e se o limite de retry foi esgotado.</summary>
    public void RecordProcessingFailure(int attempts, string error, DateTimeOffset occurredAt, bool deadLettered)
    {
        ProcessingAttempts = Math.Max(ProcessingAttempts, attempts);
        LastProcessingError = string.IsNullOrWhiteSpace(error) ? "Falha técnica sem detalhes." : error[..Math.Min(error.Length, 2048)];
        DeadLetteredAt = deadLettered ? occurredAt.ToUniversalTime() : null;
    }

    /// <summary>Limpa o diagnóstico técnico quando a mensagem é processada com sucesso.</summary>
    public void MarkProcessingSucceeded()
    {
        ProcessingAttempts = 0;
        LastProcessingError = null;
        DeadLetteredAt = null;
    }

    /// <summary>Cria uma mensagem de outbox para despacho de uma transferência agendada.</summary>
    public static TransferOutboxMessage Create(Guid transferId, DateTimeOffset availableAt, DateTimeOffset createdAt)
    {
        if (transferId == Guid.Empty)
        {
            throw new ArgumentException("A mensagem precisa referenciar uma transferência válida.", nameof(transferId));
        }

        return new TransferOutboxMessage(Guid.CreateVersion7(), transferId, availableAt, createdAt);
    }
}
