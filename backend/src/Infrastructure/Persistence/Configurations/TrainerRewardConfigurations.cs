using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class TrainerRewardSettingsConfiguration : IEntityTypeConfiguration<TrainerRewardSettings>
{
    public void Configure(EntityTypeBuilder<TrainerRewardSettings> builder)
    {
        builder.ToTable("TrainerRewardSettings", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
        builder.HasIndex(entity => entity.TenantId).IsUnique();
    }
}

public sealed class TrainerTargetDefinitionConfiguration : IEntityTypeConfiguration<TrainerTargetDefinition>
{
    public void Configure(EntityTypeBuilder<TrainerTargetDefinition> builder)
    {
        builder.ToTable("TrainerTargetDefinitions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.Property(entity => entity.Metric).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Period).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.DefaultGoal).HasPrecision(19, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique();
    }
}

public sealed class TrainerTargetAssignmentConfiguration : IEntityTypeConfiguration<TrainerTargetAssignment>
{
    public void Configure(EntityTypeBuilder<TrainerTargetAssignment> builder)
    {
        builder.ToTable("TrainerTargetAssignments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Period).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.SnapshotName).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.SnapshotMetric).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.SnapshotPeriod).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.SnapshotRewardRule).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.GoalValue).HasPrecision(19, 4);
        builder.Property(entity => entity.SnapshotDefaultGoal).HasPrecision(19, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
        builder.HasIndex(entity => new { entity.TenantId, entity.TrainerProfileId, entity.Status });
    }
}

public sealed class TrainerRewardTransactionConfiguration : IEntityTypeConfiguration<TrainerRewardTransaction>
{
    public void Configure(EntityTypeBuilder<TrainerRewardTransaction> builder)
    {
        builder.ToTable("TrainerRewardTransactions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TransactionType).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.ApprovalStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
        builder.HasIndex(entity => new { entity.TenantId, entity.IdempotencyKey }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.TrainerProfileId, entity.ApprovalStatus });
    }
}
