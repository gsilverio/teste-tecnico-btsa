using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Domain.AccountHolders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Banks;
using TesteTecnico.Api.Domain.TransferLimits;
using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Infrastructure.Persistence;

/// <summary>Contexto de persistência das contas, bancos e transferências.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Contas bancárias cadastradas.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Titulares cadastrados.</summary>
    public DbSet<AccountHolder> AccountHolders => Set<AccountHolder>();

    /// <summary>Bancos cadastrados.</summary>
    public DbSet<Bank> Banks => Set<Bank>();

    /// <summary>Transferências solicitadas.</summary>
    public DbSet<Transfer> Transfers => Set<Transfer>();

    /// <summary>Tentativas de execução usadas para aplicar os limites por hora.</summary>
    public DbSet<TransferAttempt> TransferAttempts => Set<TransferAttempt>();

    /// <summary>Auditoria das requisições anotadas e dos eventos de transferência.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Mensagens ainda não confirmadas pelo RabbitMQ.</summary>
    public DbSet<TransferOutboxMessage> TransferOutboxMessages => Set<TransferOutboxMessage>();

    /// <summary>Políticas de limite de transferência associadas às contas.</summary>
    public DbSet<TransferLimitPolicy> TransferLimitPolicies => Set<TransferLimitPolicy>();

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var transfers = PersistDomainEventsAsAuditEntries();
        var saved = base.SaveChanges(acceptAllChangesOnSuccess);
        ClearDomainEvents(transfers);
        return saved;
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var transfers = PersistDomainEventsAsAuditEntries();
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        ClearDomainEvents(transfers);
        return saved;
    }

    /// <summary>Configura o provedor PostgreSQL e os enums persistidos como tipos nativos.</summary>
    /// <param name="options">Opções do contexto.</param>
    /// <param name="connectionString">String de conexão PostgreSQL.</param>
    public static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, ConfigureNpgsqlEnums);
    }

    /// <summary>Registra os tipos ENUM e aplica os mapeamentos do modelo.</summary>
    /// <param name="modelBuilder">Construtor do modelo EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureNpgsqlEnums(NpgsqlDbContextOptionsBuilder options)
    {
        options
            .MapEnum<AuditActionType>("audit_action_type")
            .MapEnum<AuditSource>("audit_source")
            .MapEnum<AccountStatus>("account_status")
            .MapEnum<BankAccountType>("bank_account_type")
            .MapEnum<PixKeyType>("pix_key_type")
            .MapEnum<TransferMethod>("transfer_method")
            .MapEnum<TransferStatus>("transfer_status");
    }

    private Transfer[] PersistDomainEventsAsAuditEntries()
    {
        var transfers = ChangeTracker.Entries<Transfer>()
            .Select(entry => entry.Entity)
            .Where(transfer => transfer.DomainEvents.Count > 0)
            .ToArray();

        foreach (var domainEvent in transfers.SelectMany(transfer => transfer.DomainEvents))
        {
            if (!AuditEntries.Local.Any(entry => entry.Id == domainEvent.EventId))
            {
                AuditEntries.Add(AuditEntry.FromDomainEvent(domainEvent));
            }
        }

        return transfers;
    }

    private static void ClearDomainEvents(IEnumerable<Transfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            transfer.ClearDomainEvents();
        }
    }
}
