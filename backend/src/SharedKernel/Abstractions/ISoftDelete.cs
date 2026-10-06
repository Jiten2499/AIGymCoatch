namespace LifeFitAI.SharedKernel.Abstractions;

public interface ISoftDelete
{
    DateTime? DeletedAt { get; }

    bool IsDeleted { get; }

    void SoftDelete(DateTime utcNow);
}
