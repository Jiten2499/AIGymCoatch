using LifeFitAI.Application.Gym;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Gym;

public sealed class GymOptions
{
    public const string SectionName = "Gym";

    public int QrTokenMinutes { get; set; } = 10;
}

public sealed class QrSettings : IQrSettings
{
    public QrSettings(IOptions<GymOptions> options) =>
        Lifetime = TimeSpan.FromMinutes(Math.Clamp(options.Value.QrTokenMinutes, 1, 60));

    public TimeSpan Lifetime { get; }
}
