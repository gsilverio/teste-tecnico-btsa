namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Mensagem mínima publicada para solicitar a execução de uma transferência persistida.</summary>
public sealed record TransferQueueMessage(Guid TransferId);

/// <summary>Nomes estáveis das filas de processamento e mensagens não processáveis.</summary>
public static class TransferQueueNames
{
    /// <summary>Fila durável de transferências prontas para execução.</summary>
    public const string Processing = "transfers.processing";

    /// <summary>Fila de mensagens inválidas ou rejeitadas sem reentrega.</summary>
    public const string DeadLetter = "transfers.processing.dead-letter";
}
