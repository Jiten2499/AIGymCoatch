using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Mobile).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Notes).HasMaxLength(2000);
        builder.Property(entity => entity.LostReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(entity => entity.InterestedPlanId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.AssignedUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.LostByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.ConvertedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.ConvertedMemberId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class LeadFollowUpConfiguration : IEntityTypeConfiguration<LeadFollowUp>
{
    public void Configure(EntityTypeBuilder<LeadFollowUp> builder)
    {
        builder.ToTable("LeadFollowUps", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Notes).HasMaxLength(2000);
        builder.Property(entity => entity.Outcome).HasMaxLength(200);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lead>().WithMany().HasForeignKey(entity => entity.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.AssignedUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeadTrialConfiguration : IEntityTypeConfiguration<LeadTrial>
{
    public void Configure(EntityTypeBuilder<LeadTrial> builder)
    {
        builder.ToTable("LeadTrials", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Notes).HasMaxLength(2000);
        builder.Property(entity => entity.Outcome).HasMaxLength(200);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lead>().WithMany().HasForeignKey(entity => entity.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}
