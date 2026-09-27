namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Identifica uma mudança relevante no ciclo de vida de uma transferência.</summary>
public enum TransferAuditAction
{
    /// <summary>A transferência imediata foi solicitada.</summary>
    Requested,

    /// <summary>A transferência foi agendada.</summary>
    Scheduled,

    /// <summary>O processamento agendado foi iniciado.</summary>
    ProcessingStarted,

    /// <summary>A transferência foi concluída.</summary>
    Completed,

    /// <summary>A transferência foi rejeitada por uma regra de negócio.</summary>
    Failed,

    /// <summary>O agendamento foi cancelado.</summary>
    Cancelled
}
