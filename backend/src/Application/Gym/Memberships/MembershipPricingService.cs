using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Memberships;

public static class MembershipPricingService
{
    public static async Task<Result<(decimal PackagePrice, decimal DiscountAmount, decimal FinalAmount, string Currency, Guid? DiscountRuleId, string? DiscountName)>> ResolveAsync(
        Guid tenantId,
        MembershipPlan plan,
        Guid? discountRuleId,
        DateOnly asOf,
        IBusinessConfigRepository configs,
        CancellationToken cancellationToken)
    {
        if (discountRuleId is null)
        {
            return Result.Success((plan.Price, 0m, plan.Price, plan.Currency, (Guid?)null, (string?)null));
        }

        var rule = await configs.GetDiscountAsync(tenantId, discountRuleId.Value, cancellationToken).ConfigureAwait(false);
        if (rule is null || !rule.IsActive || !MembershipPricing.AppliesToPackage(rule, plan))
        {
            return Result.Failure<(decimal, decimal, decimal, string, Guid?, string?)>(GymErrors.NotFound);
        }

        if ((rule.StartDate is DateOnly start && asOf < start) || (rule.EndDate is DateOnly end && asOf > end))
        {
            return Result.Failure<(decimal, decimal, decimal, string, Guid?, string?)>(GymErrors.InvalidState);
        }

        var snapshot = MembershipPricing.Snapshot(plan.Price, rule, asOf);
        return Result.Success<(decimal, decimal, decimal, string, Guid?, string?)>((
            plan.Price,
            snapshot.DiscountAmount,
            snapshot.FinalAmount,
            plan.Currency,
            (Guid?)rule.Id,
            snapshot.DiscountName));
    }
}
