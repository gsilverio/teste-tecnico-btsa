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

    /// <summary>Marca a mensagem como publicada após confirmação do RabbitMQ.</summary>
    public void MarkPublished(DateTimeOffset publishedAt) => PublishedAt = publishedAt.ToUniversalTime();

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
