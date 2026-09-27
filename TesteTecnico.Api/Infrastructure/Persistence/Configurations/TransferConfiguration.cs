using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento persistente de transferências.</summary>
public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers", table =>
        {
            table.HasCheckConstraint("ck_transfers_positive_amount", "\"Amount\" > 0");
            table.HasCheckConstraint("ck_transfers_amount_precision", "\"Amount\" = round(\"Amount\", 2)");
            table.HasCheckConstraint("ck_transfers_different_accounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
            table.HasCheckConstraint(
                "ck_transfers_idempotency_values_paired",
                "(\"IdempotencyKey\" IS NULL) = (\"RequestFingerprint\" IS NULL)");
        });

        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Amount).HasPrecision(18, 2);
        builder.Property(transfer => transfer.Method).HasColumnType("transfer_method");
        builder.Property(transfer => transfer.Status).HasColumnType("transfer_status");
        builder.Property(transfer => transfer.FailureCode).HasMaxLength(80);
        builder.Property(transfer => transfer.IdempotencyKey).HasMaxLength(200);
        builder.Property(transfer => transfer.RequestFingerprint).HasMaxLength(64).IsFixedLength();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transfer => new { transfer.SourceAccountId, transfer.CreatedAt });
        builder.HasIndex(transfer => new { transfer.Status, transfer.ScheduledAt });
        builder.HasIndex(transfer => new { transfer.SourceAccountId, transfer.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
