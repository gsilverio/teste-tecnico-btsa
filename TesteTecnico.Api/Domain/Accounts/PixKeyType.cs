namespace TesteTecnico.Api.Domain.Accounts;

/// <summary>Tipo de chave Pix associada a uma conta.</summary>
public enum PixKeyType
{
    /// <summary>Cadastro de pessoa física com 11 dígitos.</summary>
    Cpf,

    /// <summary>Cadastro nacional de pessoa jurídica com 14 dígitos.</summary>
    Cnpj,

    /// <summary>Endereço de e-mail.</summary>
    Email,

    /// <summary>Número de telefone em formato internacional.</summary>
    Phone,

    /// <summary>Chave aleatória UUID.</summary>
    Random
}
