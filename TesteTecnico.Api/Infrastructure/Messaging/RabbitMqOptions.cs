using System.ComponentModel.DataAnnotations;

namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Configura a conexão da API com o broker RabbitMQ.</summary>
public sealed class RabbitMqOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Nome DNS ou endereço do broker.</summary>
    [Required]
    public string HostName { get; init; } = string.Empty;

    /// <summary>Porta AMQP do broker.</summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    /// <summary>Usuário AMQP.</summary>
    [Required]
    public string UserName { get; init; } = string.Empty;

    /// <summary>Senha AMQP.</summary>
    [Required]
    public string Password { get; init; } = string.Empty;

    /// <summary>Virtual host RabbitMQ.</summary>
    [Required]
    public string VirtualHost { get; init; } = "/";
}
