using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.SubscriberType).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Notes).HasMaxLength(500);
        builder.Property(entity => entity.Seats).HasDefaultValue(0);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.TenantId);
        builder.HasIndex(entity => entity.UserId);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class SubscriptionSeatConfiguration : IEntityTypeConfiguration<SubscriptionSeat>
{
    public void Configure(EntityTypeBuilder<SubscriptionSeat> builder)
    {
        builder.ToTable("SubscriptionSeats", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => new { entity.SubscriptionId, entity.UserId });
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.CreatedByIp).HasMaxLength(64);
        builder.Property(entity => entity.RevokedByIp).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Entity).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.IpAddress).HasMaxLength(64).HasColumnName("IPAddress");
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.Property(entity => entity.CorrelationId).HasMaxLength(64);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Operation).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
