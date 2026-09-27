using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Banks;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento persistente de bancos.</summary>
public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("banks");
        builder.HasKey(bank => bank.Id);
        builder.Property(bank => bank.Name).HasMaxLength(120).IsRequired();
        builder.Property(bank => bank.Ispb).HasMaxLength(8).IsRequired();
        builder.Property(bank => bank.CompeCode).HasMaxLength(3);
        builder.HasIndex(bank => bank.Ispb).IsUnique();
        builder.HasIndex(bank => bank.CompeCode).IsUnique();
    }
}
