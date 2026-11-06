using LifeFitAI.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MessageType).HasMaxLength(100);
        builder.Property(item => item.Payload).HasColumnType("nvarchar(max)");
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.CorrelationId).HasMaxLength(100);
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200);
        builder.Property(item => item.LastError).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.Status, item.CreatedAtUtc });
    }
}
