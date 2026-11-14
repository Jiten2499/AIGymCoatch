using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class WorkoutProgramConfiguration : IEntityTypeConfiguration<WorkoutProgram>
{
    public void Configure(EntityTypeBuilder<WorkoutProgram> builder)
    {
        builder.ToTable("WorkoutPrograms", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.GoalType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Difficulty).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.PromptVersion).HasMaxLength(40);
        builder.Property(entity => entity.GenerationModel).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<FitnessAssessment>().WithMany().HasForeignKey(entity => entity.SourceAssessmentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class WorkoutProgramDayConfiguration : IEntityTypeConfiguration<WorkoutProgramDay>
{
    public void Configure(EntityTypeBuilder<WorkoutProgramDay> builder)
    {
        builder.ToTable("WorkoutProgramDays", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<WorkoutProgram>().WithMany().HasForeignKey(entity => entity.WorkoutProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class WorkoutProgramExerciseConfiguration : IEntityTypeConfiguration<WorkoutProgramExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutProgramExercise> builder)
    {
        builder.ToTable("WorkoutProgramExercises", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TargetDistance).HasPrecision(10, 2);
        builder.Property(entity => entity.TargetWeight).HasPrecision(8, 2);
        builder.Property(entity => entity.Notes).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<WorkoutProgramDay>().WithMany().HasForeignKey(entity => entity.WorkoutProgramDayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(entity => entity.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.ToTable("WorkoutSessions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkoutProgram>().WithMany().HasForeignKey(entity => entity.WorkoutProgramId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkoutProgramDay>().WithMany().HasForeignKey(entity => entity.WorkoutProgramDayId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class WorkoutSessionExerciseConfiguration : IEntityTypeConfiguration<WorkoutSessionExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutSessionExercise> builder)
    {
        builder.ToTable("WorkoutSessionExercises", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Notes).HasMaxLength(500);
        builder.HasOne<WorkoutSession>().WithMany().HasForeignKey(entity => entity.WorkoutSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(entity => entity.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutSetConfiguration : IEntityTypeConfiguration<WorkoutSet>
{
    public void Configure(EntityTypeBuilder<WorkoutSet> builder)
    {
        builder.ToTable("WorkoutSets", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Weight).HasPrecision(8, 2);
        builder.Property(entity => entity.Distance).HasPrecision(10, 2);
        builder.Property(entity => entity.Rpe).HasPrecision(3, 1);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<WorkoutSessionExercise>().WithMany().HasForeignKey(entity => entity.WorkoutSessionExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}
