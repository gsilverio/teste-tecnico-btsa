namespace TesteTecnico.Api.Features.TransferLimits.Shared;

/// <summary>Política de limites de uma conta, sem expor a entidade de persistência.</summary>
public sealed record TransferLimitPolicyResponse(
    Guid Id,
    Guid AccountId,
    string AccountHolderName,
    decimal DayMaximumAmount,
    int DayMaximumAttempts,
    decimal NightMaximumAmount,
    int NightMaximumAttempts);

/// <summary>Valores máximos de transferência nos períodos diurno e noturno.</summary>
public sealed record TransferLimitPolicyValuesRequest(
    decimal DayMaximumAmount,
    int DayMaximumAttempts,
    decimal NightMaximumAmount,
    int NightMaximumAttempts);

/// <summary>Valores de uma nova política de limite e a conta à qual ela será aplicada.</summary>
public sealed record CreateTransferLimitPolicyRequest(
    Guid AccountId,
    decimal DayMaximumAmount,
    int DayMaximumAttempts,
    decimal NightMaximumAmount,
    int NightMaximumAttempts);
