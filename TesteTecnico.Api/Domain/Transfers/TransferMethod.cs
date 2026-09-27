namespace TesteTecnico.Api.Domain.Transfers;

/// <summary>Forma de identificação do destino de uma transferência.</summary>
public enum TransferMethod
{
    /// <summary>Identificação do destino por chave Pix.</summary>
    Pix,

    /// <summary>Identificação do destino por banco, agência e conta.</summary>
    BankAccount
}
