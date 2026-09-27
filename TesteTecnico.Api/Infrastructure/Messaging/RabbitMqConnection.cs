using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TesteTecnico.Api.Infrastructure.Messaging;

/// <summary>Abre e mantém uma conexão RabbitMQ durante a vida da API.</summary>
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IHostedService, IAsyncDisposable
{
    private IConnection? _connection;

    /// <summary>Obtém a conexão aberta para os serviços de mensageria da aplicação.</summary>
    public IConnection Connection => _connection
        ?? throw new InvalidOperationException("A conexão RabbitMQ ainda não foi iniciada.");

    /// <summary>Conecta ao RabbitMQ configurado antes de a API iniciar o tráfego.</summary>
    /// <param name="cancellationToken">Token de cancelamento do host.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        logger.LogDebug("Connecting to RabbitMQ at {HostName}:{Port}", settings.HostName, settings.Port);

        try
        {
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation("RabbitMQ connection established at {HostName}:{Port}", settings.HostName, settings.Port);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to connect to RabbitMQ at {HostName}:{Port}", settings.HostName, settings.Port);
            throw;
        }
    }

    /// <summary>Fecha a conexão RabbitMQ durante o encerramento do host.</summary>
    /// <param name="cancellationToken">Token de cancelamento do host.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } connection)
        {
            logger.LogDebug("Closing RabbitMQ connection");
            await connection.CloseAsync(cancellationToken);
            logger.LogInformation("RabbitMQ connection closed");
        }
    }

    /// <summary>Libera os recursos da conexão RabbitMQ.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
