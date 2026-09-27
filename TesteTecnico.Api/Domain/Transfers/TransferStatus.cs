namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Estado de execução de uma transferência.</summary>
public enum TransferStatus
{
    /// <summary>A transferência aguarda a data de execução.</summary>
    Scheduled,

    /// <summary>A transferência foi aceita e aguarda ou está em execução.</summary>
    Processing,

    /// <summary>As contas foram movimentadas com sucesso.</summary>
    Completed,

    /// <summary>A execução terminou sem movimentar as contas.</summary>
    Failed,

    /// <summary>O titular cancelou a transferência antes da execução.</summary>
    Cancelled
}
