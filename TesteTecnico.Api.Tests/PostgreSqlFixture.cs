using Microsoft.EntityFrameworkCore;
using Npgsql;
using TesteTecnico.Api.Infrastructure.Persistence;
using Xunit;

namespace TesteTecnico.Api.Tests;

[CollectionDefinition("PostgreSQL transfer processing", DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL transfer processing";
}

/// <summary>Executa testes de persistência em PostgreSQL descartável.</summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private string? _connectionString;

    /// <summary>Obtém a conexão do banco temporário.</summary>
    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("BTSA_TEST_CONNECTION não foi configurada.");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("BTSA_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var databaseName = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Os testes apagam dados; BTSA_TEST_CONNECTION deve apontar para um banco cujo nome termina em _test.");
        }

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Cria um contexto isolado conectado ao banco da fixture.</summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.ConfigureNpgsql(options, ConnectionString);
        return new AppDbContext(options.Options);
    }

    /// <summary>Remove os dados entre casos de teste preservando as migrations aplicadas.</summary>
    public async Task ResetAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                "audit_log",
                "transfer_attempts",
                "transfer_outbox_messages",
                "transfers",
                "transfer_limit_policies",
                "account_pix_keys",
                "accounts",
                "account_holders",
                "banks"
            RESTART IDENTITY CASCADE
            """);
    }
}
