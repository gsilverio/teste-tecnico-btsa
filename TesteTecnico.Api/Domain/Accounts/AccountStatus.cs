namespace TesteTecnico.Api.Domain.Accounts;

/// <summary>Situação operacional de uma conta bancária.</summary>
public enum AccountStatus
{
    /// <summary>A conta pode enviar e receber transferências.</summary>
    Active,

    /// <summary>A conta está temporariamente impedida de movimentar valores.</summary>
    Blocked,

    /// <summary>A conta está inativa e não pode movimentar valores.</summary>
    Inactive
}
