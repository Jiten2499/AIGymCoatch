using LifeFitAI.Application.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Pt;

internal static class PtPackageCurrency
{
    public static async Task<Result<string>> ResolveAsync(IBusinessProfileRepository profiles, Guid tenantId, string requested, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currency = (profile?.Currency ?? requested).Trim().ToUpperInvariant();
        if (profile is not null && !string.Equals(profile.Currency, requested.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<string>(GymErrors.InvalidState);
        }

        return currency.Length == 3
            ? Result.Success(currency)
            : Result.Failure<string>(GymErrors.InvalidState);
    }
}
