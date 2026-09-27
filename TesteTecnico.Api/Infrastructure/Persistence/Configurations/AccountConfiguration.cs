using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Banks;
using TesteTecnico.Api.Domain.AccountHolders;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento persistente de contas e respectivas chaves Pix.</summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint("ck_accounts_balance_precision", "\"Balance\" = round(\"Balance\", 2)");
            table.HasCheckConstraint("ck_accounts_overdraft_nonnegative", "\"OverdraftLimit\" >= 0");
            table.HasCheckConstraint("ck_accounts_balance_within_overdraft", "\"Balance\" >= -\"OverdraftLimit\"");
        });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Branch).HasMaxLength(8).IsRequired();
        builder.Property(account => account.Number).HasMaxLength(20).IsRequired();
        builder.Property(account => account.CheckDigit).HasMaxLength(1);
        builder.Property(account => account.Balance).HasPrecision(18, 2);
        builder.Property(account => account.OverdraftLimit).HasPrecision(18, 2);
        builder.Property(account => account.Status).HasColumnType("account_status");
        builder.Property(account => account.Type).HasColumnType("bank_account_type");

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(account => account.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountHolder>()
            .WithMany()
            .HasForeignKey(account => account.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => account.OwnerId);
        builder.HasIndex(account => new { account.BankId, account.Branch, account.Number }).IsUnique();

        builder.OwnsMany(account => account.PixKeys, pixKey =>
        {
            pixKey.ToTable("account_pix_keys");
            pixKey.WithOwner().HasForeignKey("AccountId");
            pixKey.Property<Guid>("Id").ValueGeneratedOnAdd();
            pixKey.HasKey("Id");
            pixKey.Property(key => key.Type).HasColumnType("pix_key_type");
            pixKey.Property(key => key.Value).HasMaxLength(320).IsRequired();
            pixKey.HasIndex(key => key.Value).IsUnique();
        });

        builder.Navigation(account => account.PixKeys)
            .HasField("_pixKeys")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
