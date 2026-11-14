using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class FitnessAssessmentConfiguration : IEntityTypeConfiguration<FitnessAssessment>
{
    public void Configure(EntityTypeBuilder<FitnessAssessment> builder)
    {
        builder.ToTable("FitnessAssessments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Gender).HasMaxLength(32);
        builder.Property(entity => entity.HeightCm).HasPrecision(6, 2);
        builder.Property(entity => entity.CurrentWeightKg).HasPrecision(8, 2);
        builder.Property(entity => entity.TargetWeightKg).HasPrecision(8, 2);
        builder.Property(entity => entity.WaistCircumferenceCm).HasPrecision(8, 2);
        builder.Property(entity => entity.BodyFatPercentage).HasPrecision(5, 2);
        builder.Property(entity => entity.Country).HasMaxLength(100);
        builder.Property(entity => entity.MeasurementUnit).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.PrimaryGoal).HasMaxLength(64);
        builder.Property(entity => entity.SecondaryGoal).HasMaxLength(64);
        builder.Property(entity => entity.GoalTimeline).HasMaxLength(32);
        builder.Property(entity => entity.FitnessLevel).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Priority).HasMaxLength(32);
        builder.Property(entity => entity.GoalDescription).HasMaxLength(1000);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
