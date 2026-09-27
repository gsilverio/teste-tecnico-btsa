using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento do histórico síncrono da API e dos fatos de domínio.</summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log", table =>
        {
            table.HasCheckConstraint("ck_audit_log_positive_amount", "\"Amount\" IS NULL OR \"Amount\" > 0");
            table.HasCheckConstraint("ck_audit_log_error_message_length", "\"ErrorMessage\" IS NULL OR length(\"ErrorMessage\") <= 2048");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Source).HasColumnType("audit_source");
        builder.Property(entry => entry.ActionType).HasColumnType("audit_action_type");
        builder.Property(entry => entry.ActorId).HasMaxLength(200);
        builder.Property(entry => entry.HttpMethod).HasMaxLength(12);
        builder.Property(entry => entry.RouteTemplate).HasMaxLength(256);
        builder.Property(entry => entry.Amount).HasPrecision(18, 2);
        builder.Property(entry => entry.Method).HasColumnType("transfer_method");
        builder.Property(entry => entry.PreviousStatus).HasColumnType("transfer_status");
        builder.Property(entry => entry.Status).HasColumnType("transfer_status");
        builder.Property(entry => entry.PayloadJson).HasColumnType("jsonb");
        builder.Property(entry => entry.ErrorMessage).HasMaxLength(2048);
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.TransferId, entry.Sequence })
            .IsUnique()
            .HasFilter("\"Sequence\" IS NOT NULL");
    }
}
