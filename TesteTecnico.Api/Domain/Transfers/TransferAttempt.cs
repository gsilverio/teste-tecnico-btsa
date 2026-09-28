namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Registra uma tentativa de execução para aplicar limites por conta e janela.</summary>
public sealed class TransferAttempt
{
    private TransferAttempt()
    {
    }

    private TransferAttempt(
        Guid id,
        Guid? transferId,
        Guid sourceAccountId,
        DateTimeOffset attemptedAt,
        string? failureCode,
        string? failureMessage,
        string? idempotencyKey,
        string? requestFingerprint)
    {
        Id = id;
        TransferId = transferId;
        SourceAccountId = sourceAccountId;
        AttemptedAt = attemptedAt.ToUniversalTime();
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
    }

    /// <summary>Identificador único do registro da tentativa.</summary>
    public Guid Id { get; private set; }

    /// <summary>Transferência que originou a tentativa.</summary>
    public Guid? TransferId { get; private set; }

    /// <summary>Conta de origem usada para agrupar os limites por conta.</summary>
    public Guid SourceAccountId { get; private set; }

    /// <summary>Instante UTC em que a tentativa foi avaliada.</summary>
    public DateTimeOffset AttemptedAt { get; private set; }

    /// <summary>Código da rejeição ocorrida antes de criar uma transferência persistida.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Mensagem segura devolvida no replay idempotente de uma rejeição prévia.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>Chave opcional para não contar novamente o replay de uma solicitação rejeitada.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Hash do pedido rejeitado associado à chave idempotente.</summary>
    public string? RequestFingerprint { get; private set; }

    /// <summary>Cria o registro da tentativa para uma transferência persistida.</summary>
    public static TransferAttempt Create(Guid transferId, Guid sourceAccountId, DateTimeOffset attemptedAt)
    {
        if (transferId == Guid.Empty || sourceAccountId == Guid.Empty)
        {
            throw new ArgumentException("A tentativa precisa referenciar uma transferência e uma conta válidas.");
        }

        return new TransferAttempt(Guid.CreateVersion7(), transferId, sourceAccountId, attemptedAt, null, null, null, null);
    }

    /// <summary>Cria registro de tentativa rejeitada antes da criação da transferência.</summary>
    public static TransferAttempt CreateRejected(
        Guid sourceAccountId,
        DateTimeOffset attemptedAt,
        string failureCode,
        string failureMessage,
        string? idempotencyKey,
        string? requestFingerprint)
    {
        if (sourceAccountId == Guid.Empty || string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("A tentativa rejeitada precisa de uma conta e motivo válidos.");
        }

        if ((idempotencyKey is null) != (requestFingerprint is null))
        {
            throw new ArgumentException("A chave e a impressão digital idempotente devem ser informadas juntas.");
        }

        return new TransferAttempt(
            Guid.CreateVersion7(),
            null,
            sourceAccountId,
            attemptedAt,
            failureCode,
            failureMessage,
            idempotencyKey,
            requestFingerprint);
    }
}
