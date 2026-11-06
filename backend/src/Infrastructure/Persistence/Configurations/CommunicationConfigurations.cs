using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class CommunicationConfiguration : IEntityTypeConfiguration<Communication>
{
    public void Configure(EntityTypeBuilder<Communication> builder)
    {
        builder.ToTable("Communications", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Title).HasMaxLength(200);
        builder.Property(item => item.Message).HasMaxLength(4000);
        builder.Property(item => item.Priority).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.AudienceType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.ImageStorageKey).HasMaxLength(500);
        builder.Property(item => item.ImageContentType).HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(item => item.AudiencePackageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerProfile>().WithMany().HasForeignKey(item => item.AudienceTrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class CommunicationAudienceMemberConfiguration : IEntityTypeConfiguration<CommunicationAudienceMember>
{
    public void Configure(EntityTypeBuilder<CommunicationAudienceMember> builder)
    {
        builder.ToTable("CommunicationAudienceMembers", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Communication>().WithMany().HasForeignKey(item => item.CommunicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(item => item.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class CommunicationAudienceTargetConfiguration : IEntityTypeConfiguration<CommunicationAudienceTarget>
{
    public void Configure(EntityTypeBuilder<CommunicationAudienceTarget> builder)
    {
        builder.ToTable("CommunicationAudienceTargets", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TargetKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Communication>().WithMany().HasForeignKey(item => item.CommunicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class CommunicationRecipientConfiguration : IEntityTypeConfiguration<CommunicationRecipient>
{
    public void Configure(EntityTypeBuilder<CommunicationRecipient> builder)
    {
        builder.ToTable("CommunicationRecipients", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RecipientKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Communication>().WithMany().HasForeignKey(item => item.CommunicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class CommunicationReadConfiguration : IEntityTypeConfiguration<CommunicationRead>
{
    public void Configure(EntityTypeBuilder<CommunicationRead> builder)
    {
        builder.ToTable("CommunicationReads", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Communication>().WithMany().HasForeignKey(item => item.CommunicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}
