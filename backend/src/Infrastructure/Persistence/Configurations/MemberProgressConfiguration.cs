using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class MemberProgressAssessmentConfiguration : IEntityTypeConfiguration<MemberProgressAssessment>
{
    public void Configure(EntityTypeBuilder<MemberProgressAssessment> builder)
    {
        builder.ToTable("MemberProgressAssessments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.UnitSystem).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Weight).HasPrecision(8, 2);
        builder.Property(entity => entity.Waist).HasPrecision(8, 2);
        builder.Property(entity => entity.Chest).HasPrecision(8, 2);
        builder.Property(entity => entity.Hip).HasPrecision(8, 2);
        builder.Property(entity => entity.Arm).HasPrecision(8, 2);
        builder.Property(entity => entity.RightArm).HasPrecision(8, 2);
        builder.Property(entity => entity.Thigh).HasPrecision(8, 2);
        builder.Property(entity => entity.RightThigh).HasPrecision(8, 2);
        builder.Property(entity => entity.Height).HasPrecision(8, 2);
        builder.Property(entity => entity.BodyFatPercentage).HasPrecision(5, 2);
        builder.Property(entity => entity.VisceralFat).HasPrecision(5, 2);
        builder.Property(entity => entity.FitnessScore).HasPrecision(5, 2);
        builder.Property(entity => entity.StrengthScore).HasPrecision(4, 1);
        builder.Property(entity => entity.EnduranceScore).HasPrecision(4, 1);
        builder.Property(entity => entity.FlexibilityScore).HasPrecision(4, 1);
        builder.Property(entity => entity.CoreScore).HasPrecision(4, 1);
        builder.Property(entity => entity.TrainingPerformance).HasMaxLength(500);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.AchievedGoals).HasMaxLength(500);
        builder.Property(entity => entity.NextGoals).HasMaxLength(500);
        builder.Property(entity => entity.PhotoStorageKey).HasMaxLength(200);
        builder.Property(entity => entity.PhotoContentType).HasMaxLength(80);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerProfile>().WithMany().HasForeignKey(entity => entity.TrainerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BodyMeasurement>().WithMany().HasForeignKey(entity => entity.BodyMeasurementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
