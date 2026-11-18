using LifeFitAI.Application.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Pt;

internal static class PtPackageCurrency
{
    public static async Task<Result<string>> ResolveAsync(IBusinessProfileRepository profiles, Guid tenantId, string requested, CancellationToken cancellationToken)
    {
        var currency = await WorkspaceCurrency.ForTenantAsync(profiles, tenantId, requested, cancellationToken).ConfigureAwait(false);
        return currency.Length == 3
            ? Result.Success(currency)
            : Result.Failure<string>(GymErrors.UnknownCurrency);
    }
}
