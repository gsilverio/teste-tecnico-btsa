using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TesteTecnico.Api.Domain.Transfers;

namespace TesteTecnico.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento persistente da outbox de mensagens de transferência.</summary>
public sealed class TransferOutboxMessageConfiguration : IEntityTypeConfiguration<TransferOutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TransferOutboxMessage> builder)
    {
        builder.ToTable("transfer_outbox_messages");
        builder.HasKey(message => message.Id);
        builder.HasIndex(message => message.TransferId).IsUnique();
        builder.HasIndex(message => new { message.PublishedAt, message.AvailableAt });
        builder.HasOne<Transfer>()
            .WithOne()
            .HasForeignKey<TransferOutboxMessage>(message => message.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
