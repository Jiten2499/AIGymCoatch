using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.ToTable("MembershipPlans", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.MembershipType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Category).HasConversion<string>().HasMaxLength(32).HasColumnName("PackageCategory");
        builder.Property(entity => entity.DurationUnit).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Price).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class GymMembershipConfiguration : IEntityTypeConfiguration<GymMembership>
{
    public void Configure(EntityTypeBuilder<GymMembership> builder)
    {
        builder.ToTable("GymMemberships", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MembershipNumber).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.PackagePrice).HasPrecision(19, 4);
        builder.Property(entity => entity.DiscountAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.FinalAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.DiscountName).HasMaxLength(200);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(entity => entity.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DiscountRule>().WithMany().HasForeignKey(entity => entity.DiscountRuleId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class MembershipPaymentConfiguration : IEntityTypeConfiguration<MembershipPayment>
{
    public void Configure(EntityTypeBuilder<MembershipPayment> builder)
    {
        builder.ToTable("MembershipPayments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Amount).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.ReferenceNumber).HasMaxLength(100);
        builder.Property(entity => entity.Notes).HasMaxLength(2000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(entity => entity.GymMembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.VoidedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class MembershipHistoryConfiguration : IEntityTypeConfiguration<MembershipHistoryEntry>
{
    public void Configure(EntityTypeBuilder<MembershipHistoryEntry> builder)
    {
        builder.ToTable("MembershipHistory", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.PreviousStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.NewStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(entity => entity.GymMembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.PerformedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MembershipFreezeConfiguration : IEntityTypeConfiguration<MembershipFreeze>
{
    public void Configure(EntityTypeBuilder<MembershipFreeze> builder)
    {
        builder.ToTable("MembershipFreezes", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.PreviousStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(entity => entity.GymMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("Attendance", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CheckInMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(entity => entity.GymMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QrCheckInTokenConfiguration : IEntityTypeConfiguration<QrCheckInToken>
{
    public void Configure(EntityTypeBuilder<QrCheckInToken> builder)
    {
        builder.ToTable("QrCheckInTokens", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
