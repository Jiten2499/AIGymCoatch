using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Fitness;

public sealed class Exercise : BaseEntity
{
    private Exercise()
    {
    }

    public Guid? TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Instructions { get; private set; }

    public MuscleGroup MuscleGroup { get; private set; }

    public string? Equipment { get; private set; }

    public ExerciseType ExerciseType { get; private set; }

    public WorkoutDifficulty Difficulty { get; private set; }

    public string? VideoUrl { get; private set; }

    public string? ImageUrl { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsSystem => TenantId is null;

    public static Exercise CreateCustom(Guid id, Guid tenantId, string name, string? description, string? instructions, MuscleGroup muscleGroup, string? equipment, ExerciseType exerciseType, WorkoutDifficulty difficulty) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description,
            Instructions = instructions,
            MuscleGroup = muscleGroup,
            Equipment = equipment,
            ExerciseType = exerciseType,
            Difficulty = difficulty,
            IsActive = true,
        };
}
