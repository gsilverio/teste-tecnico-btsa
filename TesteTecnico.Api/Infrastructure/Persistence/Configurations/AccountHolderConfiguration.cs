using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.AccountHolders;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento persistente de titulares.</summary>
public sealed class AccountHolderConfiguration : IEntityTypeConfiguration<AccountHolder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountHolder> builder)
    {
        builder.ToTable("account_holders");
        builder.HasKey(holder => holder.Id);
        builder.Property(holder => holder.Name).HasMaxLength(120).IsRequired();
        builder.Property(holder => holder.Cpf).HasMaxLength(11).IsRequired();
        builder.HasIndex(holder => holder.Cpf).IsUnique();
    }
}
