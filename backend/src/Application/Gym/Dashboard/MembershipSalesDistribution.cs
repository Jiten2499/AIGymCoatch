using FluentValidation;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Dashboard;

public sealed record MembershipSalesSlice(Guid PlanId, string PackageName, int Sold, decimal Percentage);

public sealed record MembershipSalesDistribution(DateOnly From, DateOnly To, int TotalSold, IReadOnlyList<MembershipSalesSlice> Slices);

public static class MembershipSalesShare
{
    public static IReadOnlyList<MembershipSalesSlice> Calculate(IReadOnlyList<MembershipSaleCount> counts)
    {
        var eligible = counts.Where(count => count.Sold > 0).OrderByDescending(count => count.Sold).ThenBy(count => count.PackageName, StringComparer.Ordinal).ToArray();
        var total = eligible.Sum(count => count.Sold);
        if (total == 0)
        {
            return [];
        }

        var slices = eligible.Select(count => new MembershipSalesSlice(count.PlanId, count.PackageName, count.Sold, decimal.Round(count.Sold * 100m / total, 1, MidpointRounding.AwayFromZero))).ToArray();
        var drift = 100m - slices.Sum(slice => slice.Percentage);
        if (drift != 0m)
        {
            slices[0] = slices[0] with { Percentage = slices[0].Percentage + drift };
        }

        return slices;
    }
}

public sealed record GetMembershipSalesDistributionQuery(DateOnly From, DateOnly To, AnalyticsPeriod Period) : IRequest<Result<MembershipSalesDistribution>>;

public sealed class GetMembershipSalesDistributionQueryValidator : AbstractValidator<GetMembershipSalesDistributionQuery>
{
    public GetMembershipSalesDistributionQueryValidator() => AnalyticsWindowRules.Apply(this, query => query.From, query => query.To, query => query.Period);
}

public sealed class GetMembershipSalesDistributionQueryHandler : IRequestHandler<GetMembershipSalesDistributionQuery, Result<MembershipSalesDistribution>>
{
    private readonly IDashboardAccess _access;
    private readonly IDashboardAnalytics _analytics;

    public GetMembershipSalesDistributionQueryHandler(IDashboardAccess access, IDashboardAnalytics analytics)
    {
        _access = access;
        _analytics = analytics;
    }

    public async Task<Result<MembershipSalesDistribution>> Handle(GetMembershipSalesDistributionQuery request, CancellationToken cancellationToken)
    {
        var opened = await _access.OpenAreaAsync("Memberships", cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure<MembershipSalesDistribution>(opened.Error);
        }

        var calendar = ResolvedAnalytics.Resolve(request.From, request.To, request.Period, opened.Value.TimeZone);
        if (calendar.IsFailure)
        {
            return Result.Failure<MembershipSalesDistribution>(calendar.Error);
        }

        var counts = await _analytics.CountMembershipSalesAsync(opened.Value.Tenant.Id, calendar.Value.From, calendar.Value.To, cancellationToken).ConfigureAwait(false);
        var slices = MembershipSalesShare.Calculate(counts);
        return Result.Success(new MembershipSalesDistribution(calendar.Value.From, calendar.Value.To, slices.Sum(slice => slice.Sold), slices));
    }
}
