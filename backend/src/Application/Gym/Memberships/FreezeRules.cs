using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Memberships;

public static class FreezeRules
{
    public static Error? Check(TenantBusinessConfig? config, int days, int existingCount)
    {
        var max = config?.MaxFreezeDays ?? 30;
        var occurrences = config?.MaxFreezeOccurrences ?? 2;
        if (max <= 0 || occurrences <= 0)
        {
            return GymErrors.FreezeNotAllowed;
        }

        if (existingCount >= occurrences)
        {
            return GymErrors.FreezeLimitReached;
        }

        if (days <= 0)
        {
            return null;
        }

        var minimum = Math.Max(config?.MinFreezeDays ?? 1, 1);
        return days < minimum || days > max ? GymErrors.FreezeOutsideLimits : null;
    }

    public static int ExtensionDays(TenantBusinessConfig? config, int elapsedDays, int? plannedDays)
    {
        if (plannedDays is > 0)
        {
            return 0;
        }

        var grant = Math.Max(0, elapsedDays);
        var max = config?.MaxFreezeDays ?? 30;
        return max > 0 && grant > max ? max : grant;
    }
}
