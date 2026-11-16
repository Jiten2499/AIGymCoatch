using LifeFitAI.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("PushDevices", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.InstallationId).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ExpoPushToken).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Platform).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.AppVersion).HasMaxLength(32);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}

public sealed class PushDeliveryConfiguration : IEntityTypeConfiguration<PushDelivery>
{
    public void Configure(EntityTypeBuilder<PushDelivery> builder)
    {
        builder.ToTable("PushDeliveries", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TicketId).HasMaxLength(120);
        builder.Property(entity => entity.ReceiptError).HasMaxLength(200);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "app");
        builder.HasKey(entity => new { entity.TenantId, entity.UserId });
    }
}
