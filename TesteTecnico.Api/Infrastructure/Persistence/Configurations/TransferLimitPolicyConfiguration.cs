using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.TransferLimits;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento da política de limites por conta.</summary>
public sealed class TransferLimitPolicyConfiguration : IEntityTypeConfiguration<TransferLimitPolicy>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransferLimitPolicy> builder)
    {
        builder.ToTable("transfer_limit_policies", table =>
        {
            table.HasCheckConstraint("ck_transfer_limit_policies_day_amount_nonnegative", "\"DayMaximumAmount\" >= 0");
            table.HasCheckConstraint("ck_transfer_limit_policies_day_attempts_nonnegative", "\"DayMaximumAttempts\" >= 0");
            table.HasCheckConstraint("ck_transfer_limit_policies_night_amount_nonnegative", "\"NightMaximumAmount\" >= 0");
            table.HasCheckConstraint("ck_transfer_limit_policies_night_attempts_nonnegative", "\"NightMaximumAttempts\" >= 0");
        });

        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.DayMaximumAmount).HasPrecision(18, 2);
        builder.Property(policy => policy.NightMaximumAmount).HasPrecision(18, 2);

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<TransferLimitPolicy>(policy => policy.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
