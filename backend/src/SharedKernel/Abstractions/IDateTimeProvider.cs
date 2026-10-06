namespace LifeFitAI.SharedKernel.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
