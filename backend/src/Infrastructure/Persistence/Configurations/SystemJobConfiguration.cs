using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class SystemJobConfiguration : IEntityTypeConfiguration<SystemJobDefinition>
{
    public void Configure(EntityTypeBuilder<SystemJobDefinition> builder)
    {
        builder.ToTable("SystemJobDefinitions", "app");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.JobKey).HasMaxLength(100).IsRequired();
        builder.Property(job => job.Name).HasMaxLength(200).IsRequired();
        builder.Property(job => job.Description).HasMaxLength(1000);
        builder.Property(job => job.JobType).HasMaxLength(200).IsRequired();
        builder.Property(job => job.CronExpression).HasMaxLength(100).IsRequired();
        builder.Property(job => job.Queue).HasMaxLength(50).IsRequired();
        builder.Property(job => job.FeatureKey).HasMaxLength(100);
        builder.Property(job => job.RowVersion).IsRowVersion();
        builder.HasIndex(job => job.JobKey).IsUnique();
    }
}

public sealed class MembershipExpiryReminderConfiguration : IEntityTypeConfiguration<MembershipExpiryReminder>
{
    public void Configure(EntityTypeBuilder<MembershipExpiryReminder> builder)
    {
        builder.ToTable("MembershipExpiryReminders", "app");
        builder.HasKey(reminder => reminder.Id);
        builder.Property(reminder => reminder.ReminderType).HasMaxLength(40).IsRequired();
        builder.Property(reminder => reminder.RowVersion).IsRowVersion();
        builder.HasIndex(reminder => new { reminder.TenantId, reminder.MembershipId, reminder.ReminderType, reminder.ExpiryDate }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(reminder => reminder.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(reminder => reminder.MembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
