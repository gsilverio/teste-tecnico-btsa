using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento dos registros de tentativas de transferência.</summary>
public sealed class TransferAttemptConfiguration : IEntityTypeConfiguration<TransferAttempt>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransferAttempt> builder)
    {
        builder.ToTable("transfer_attempts");
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.FailureCode).HasMaxLength(80);
        builder.Property(attempt => attempt.FailureMessage).HasMaxLength(2048);
        builder.Property(attempt => attempt.IdempotencyKey).HasMaxLength(200);
        builder.Property(attempt => attempt.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(attempt => attempt.TransferId)
            .IsUnique()
            .HasFilter("\"TransferId\" IS NOT NULL");
        builder.HasIndex(attempt => new { attempt.SourceAccountId, attempt.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(attempt => new { attempt.SourceAccountId, attempt.AttemptedAt });
        builder.HasOne<Transfer>()
            .WithOne()
            .HasForeignKey<TransferAttempt>(attempt => attempt.TransferId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(attempt => attempt.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
