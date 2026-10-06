using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Instructions).HasMaxLength(2000);
        builder.Property(entity => entity.MuscleGroup).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Equipment).HasMaxLength(100);
        builder.Property(entity => entity.ExerciseType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Difficulty).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.VideoUrl).HasMaxLength(500);
        builder.Property(entity => entity.ImageUrl).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class FitnessProfileConfiguration : IEntityTypeConfiguration<FitnessProfile>
{
    public void Configure(EntityTypeBuilder<FitnessProfile> builder)
    {
        builder.ToTable("FitnessProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.UnitSystem).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Height).HasPrecision(6, 2);
        builder.Property(entity => entity.Weight).HasPrecision(8, 2);
        builder.Property(entity => entity.FitnessLevel).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.PrimaryGoal).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.ActivityLevel).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TrainingExperience).HasMaxLength(500);
        builder.Property(entity => entity.PreferredTrainingLocation).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class FitnessGoalConfiguration : IEntityTypeConfiguration<FitnessGoal>
{
    public void Configure(EntityTypeBuilder<FitnessGoal> builder)
    {
        builder.ToTable("FitnessGoals", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.GoalType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TargetValue).HasPrecision(12, 2);
        builder.Property(entity => entity.CurrentValue).HasPrecision(12, 2);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        builder.ToTable("BodyMeasurements", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.UnitSystem).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Weight).HasPrecision(8, 2);
        builder.Property(entity => entity.BodyFatPercentage).HasPrecision(5, 2);
        builder.Property(entity => entity.Chest).HasPrecision(8, 2);
        builder.Property(entity => entity.Waist).HasPrecision(8, 2);
        builder.Property(entity => entity.Hip).HasPrecision(8, 2);
        builder.Property(entity => entity.Arm).HasPrecision(8, 2);
        builder.Property(entity => entity.Thigh).HasPrecision(8, 2);
        builder.Property(entity => entity.Notes).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BodyScanRecordConfiguration : IEntityTypeConfiguration<BodyScanRecord>
{
    public void Configure(EntityTypeBuilder<BodyScanRecord> builder)
    {
        builder.ToTable("BodyScanRecords", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.StorageReference).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
