namespace LifeFitAI.Application.Gym;

internal static class WorkspaceCurrency
{
    public static async Task<string> ForTenantAsync(IBusinessProfileRepository profiles, Guid tenantId, string requested, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currency = string.IsNullOrWhiteSpace(profile?.Currency) ? requested : profile!.Currency;
        var code = currency.Trim().ToUpperInvariant();
        return code.Length == 3 ? code : "USD";
    }
}
