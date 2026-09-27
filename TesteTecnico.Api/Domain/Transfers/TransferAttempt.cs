namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Registra uma tentativa de execução para aplicar limites por conta e janela.</summary>
public sealed class TransferAttempt
{
    private TransferAttempt()
    {
    }

    private TransferAttempt(Guid id, Guid transferId, Guid sourceAccountId, DateTimeOffset attemptedAt)
    {
        Id = id;
        TransferId = transferId;
        SourceAccountId = sourceAccountId;
        AttemptedAt = attemptedAt.ToUniversalTime();
    }

    /// <summary>Identificador único do registro da tentativa.</summary>
    public Guid Id { get; private set; }

    /// <summary>Transferência que originou a tentativa.</summary>
    public Guid TransferId { get; private set; }

    /// <summary>Conta de origem usada para agrupar os limites por conta.</summary>
    public Guid SourceAccountId { get; private set; }

    /// <summary>Instante UTC em que a tentativa foi avaliada.</summary>
    public DateTimeOffset AttemptedAt { get; private set; }

    /// <summary>Cria o registro da tentativa para uma transferência persistida.</summary>
    public static TransferAttempt Create(Guid transferId, Guid sourceAccountId, DateTimeOffset attemptedAt)
    {
        if (transferId == Guid.Empty || sourceAccountId == Guid.Empty)
        {
            throw new ArgumentException("A tentativa precisa referenciar uma transferência e uma conta válidas.");
        }

        return new TransferAttempt(Guid.CreateVersion7(), transferId, sourceAccountId, attemptedAt);
    }
}
