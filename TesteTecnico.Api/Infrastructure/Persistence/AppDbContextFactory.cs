using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TesteTecnico.Api.Infrastructure.Persistence;

/// <summary>Cria o contexto em tempo de design para comandos do EF Core.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=teste_tecnico_btsa;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.ConfigureNpgsql(options, connectionString);
        return new AppDbContext(options.Options);
    }
}
