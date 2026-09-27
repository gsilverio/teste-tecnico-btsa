namespace TesteTecnico.Api.Domain.Audit;

/// <summary>Ações auditáveis da API e fatos relevantes de transferência.</summary>
public enum AuditActionType
{
    /// <summary>Solicitou uma transferência imediata.</summary>
    RequestTransfer,

    /// <summary>Agendou uma transferência.</summary>
    ScheduleTransfer,

    /// <summary>Cancelou uma transferência agendada.</summary>
    CancelTransfer,

    /// <summary>Alterou a situação de uma conta.</summary>
    SetAccountStatus,

    /// <summary>Alterou o cheque especial de uma conta.</summary>
    SetAccountOverdraft,

    /// <summary>Removeu o cheque especial de uma conta.</summary>
    ClearAccountOverdraft,

    /// <summary>Criou limites para uma conta.</summary>
    CreateTransferLimitPolicy,

    /// <summary>Atualizou os limites de uma conta.</summary>
    UpdateTransferLimitPolicy,

    /// <summary>Removeu os limites de uma conta.</summary>
    DeleteTransferLimitPolicy,

    /// <summary>Consultou os dados de uma conta específica.</summary>
    ViewAccountDetails,

    /// <summary>Consultou a configuração de cheque especial de uma conta.</summary>
    ViewAccountOverdraft,

    /// <summary>O domínio recebeu uma solicitação de transferência imediata.</summary>
    TransferRequested,

    /// <summary>O domínio criou um agendamento de transferência.</summary>
    TransferScheduled,

    /// <summary>O domínio iniciou o processamento de uma transferência agendada.</summary>
    TransferProcessingStarted,

    /// <summary>O domínio concluiu a transferência.</summary>
    TransferCompleted,

    /// <summary>O domínio rejeitou a transferência por uma regra de negócio.</summary>
    TransferFailed,

    /// <summary>O domínio cancelou o agendamento da transferência.</summary>
    TransferCancelled
}
