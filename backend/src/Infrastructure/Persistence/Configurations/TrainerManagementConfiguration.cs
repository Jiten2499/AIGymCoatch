using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class TrainerMemberAssignmentConfiguration : IEntityTypeConfiguration<TrainerMemberAssignment>
{
    public void Configure(EntityTypeBuilder<TrainerMemberAssignment> builder)
    {
        builder.ToTable("TrainerMemberAssignments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
        builder.HasIndex(entity => new { entity.TenantId, entity.MemberProfileId });
    }
}

public sealed class TrainerTargetConfiguration : IEntityTypeConfiguration<TrainerTarget>
{
    public void Configure(EntityTypeBuilder<TrainerTarget> builder)
    {
        builder.ToTable("TrainerTargets", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TargetType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Period).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.TargetValue).HasPrecision(19, 4);
        builder.Property(entity => entity.Name).HasMaxLength(120);
        builder.Property(entity => entity.IsActive);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class TrainerIncentiveRuleConfiguration : IEntityTypeConfiguration<TrainerIncentiveRule>
{
    public void Configure(EntityTypeBuilder<TrainerIncentiveRule> builder)
    {
        builder.ToTable("TrainerIncentiveRules", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TargetType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Percent).HasColumnName("IncentivePercent").HasPrecision(9, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class TrainerCommissionRuleConfiguration : IEntityTypeConfiguration<TrainerCommissionRule>
{
    public void Configure(EntityTypeBuilder<TrainerCommissionRule> builder)
    {
        builder.ToTable("TrainerCommissionRules", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(entity => entity.Percent).HasPrecision(9, 4);
        builder.Property(entity => entity.FixedAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class TrainerPerformanceIncentiveConfiguration : IEntityTypeConfiguration<TrainerPerformanceIncentive>
{
    public void Configure(EntityTypeBuilder<TrainerPerformanceIncentive> builder)
    {
        builder.ToTable("TrainerPerformanceIncentives", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Metric).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RewardKind).HasConversion<string>().HasMaxLength(40);
        builder.Property(entity => entity.Threshold).HasPrecision(19, 4);
        builder.Property(entity => entity.FixedAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.Percent).HasPrecision(9, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class TrainerCompensationCalculationConfiguration : IEntityTypeConfiguration<TrainerCompensationCalculation>
{
    public void Configure(EntityTypeBuilder<TrainerCompensationCalculation> builder)
    {
        builder.ToTable("TrainerCompensationCalculations", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(entity => entity.SourceKey).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Basis).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Amount).HasPrecision(19, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
