using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class TenantBusinessConfigConfiguration : IEntityTypeConfiguration<TenantBusinessConfig>
{
    public void Configure(EntityTypeBuilder<TenantBusinessConfig> builder)
    {
        builder.ToTable("TenantBusinessConfigs", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DefaultCheckInMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class MemberCategoryConfiguration : IEntityTypeConfiguration<MemberCategory>
{
    public void Configure(EntityTypeBuilder<MemberCategory> builder)
    {
        builder.ToTable("MemberCategories", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class DiscountRuleConfiguration : IEntityTypeConfiguration<DiscountRule>
{
    public void Configure(EntityTypeBuilder<DiscountRule> builder)
    {
        builder.ToTable("DiscountRules", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.Property(entity => entity.DiscountType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.AppliesTo).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.PackageCategory).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Value).HasPrecision(19, 4);
        builder.Property(entity => entity.MinimumPackagePrice).HasPrecision(19, 4);
        builder.Property(entity => entity.MaximumDiscountAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(entity => entity.PackageId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberCategory>().WithMany().HasForeignKey(entity => entity.MemberCategoryId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class CrmLeadSourceConfiguration : IEntityTypeConfiguration<CrmLeadSource>
{
    public void Configure(EntityTypeBuilder<CrmLeadSource> builder)
    {
        builder.ToTable("CrmLeadSources", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Code).HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class CrmLostReasonConfiguration : IEntityTypeConfiguration<CrmLostReason>
{
    public void Configure(EntityTypeBuilder<CrmLostReason> builder)
    {
        builder.ToTable("CrmLostReasons", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Code).HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
