using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class PtSessionExercise : BaseEntity
{
    private PtSessionExercise()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PtSessionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? MuscleGroup { get; private set; }

    public int Sets { get; private set; }

    public string Reps { get; private set; } = string.Empty;

    public bool Completed { get; private set; }

    public int SortOrder { get; private set; }

    public static PtSessionExercise? Create(Guid id, PtSession session, string name, string? muscleGroup, int sets, string reps, int sortOrder)
    {
        if (session.Status != PtSessionStatus.Scheduled || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || sets is < 1 or > 20 || string.IsNullOrWhiteSpace(reps) || reps.Trim().Length > 40)
        {
            return null;
        }

        return new PtSessionExercise
        {
            Id = id,
            TenantId = session.TenantId,
            PtSessionId = session.Id,
            Name = name.Trim(),
            MuscleGroup = string.IsNullOrWhiteSpace(muscleGroup) ? null : muscleGroup.Trim(),
            Sets = sets,
            Reps = reps.Trim(),
            SortOrder = sortOrder,
        };
    }

    public bool Toggle(PtSession session)
    {
        if (session.Id != PtSessionId || session.Status != PtSessionStatus.Scheduled)
        {
            return false;
        }

        Completed = !Completed;
        return true;
    }
}
