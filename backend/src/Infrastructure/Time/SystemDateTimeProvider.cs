using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
