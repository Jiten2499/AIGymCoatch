using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Tests.Identity;

/// <summary>
/// In-memory plan catalogue for tests. Provides the PERSONAL_FREE and BUSINESS_FREE plans
/// and exposes their features so the entitlement store can compute access purely from the
/// plan, matching the production (plan-driven) model.
/// </summary>
internal sealed class PersonalFreePlans : IPlanDefinitionRepository
{
    private static readonly string[] PersonalEnabled =
    [
        FeatureCodes.WorkoutPlans,
        FeatureCodes.AiCoach,
        FeatureCodes.AiWorkoutGeneration,
        FeatureCodes.AiProgressInsights,
    ];

    private readonly Dictionary<Guid, PlanDefinition> _plansById = [];
    private readonly Dictionary<string, PlanDefinition> _plansByCode = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, List<PlanFeature>> _featuresByPlan = [];

    private PersonalFreePlans()
    {
        AddPlan(Guid.Parse("a1111111-1111-4111-8111-111111111101"), SubscriptionIssuance.PersonalFree, "Personal Free", "Personal", PersonalEnabled);
        AddPlan(Guid.Parse("a1111111-1111-4111-8111-111111111102"), SubscriptionIssuance.BusinessFree, "Business Free", "Business", FeatureCodes.All.ToArray());
    }

    public static PersonalFreePlans Create() => new();

    public Guid PersonalPlanId => _plansByCode[SubscriptionIssuance.PersonalFree].Id;

    public Guid BusinessPlanId => _plansByCode[SubscriptionIssuance.BusinessFree].Id;

    public Task<PlanDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_plansByCode.GetValueOrDefault(code));

    public Task<IReadOnlyList<PlanFeature>> GetFeaturesAsync(Guid planId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlanFeature>>(_featuresByPlan.GetValueOrDefault(planId) ?? []);

    public bool? PlanFeatureEnabled(Guid planId, string featureCode)
    {
        if (!_featuresByPlan.TryGetValue(planId, out var features))
        {
            return null;
        }

        return features.FirstOrDefault(feature => feature.FeatureCode == featureCode)?.Enabled ?? false;
    }

    private void AddPlan(Guid id, string code, string name, string appliesTo, IEnumerable<string> enabled)
    {
        var plan = PlanDefinition.Create(id, code, name, null, appliesTo, 1, 0);
        plan.Activate();
        var enabledSet = new HashSet<string>(enabled, StringComparer.Ordinal);
        var features = FeatureCodes.All
            .Select(featureCode => PlanFeature.Create(Guid.NewGuid(), plan.Id, featureCode, enabledSet.Contains(featureCode), DateTime.UtcNow))
            .ToList();
        _plansById[id] = plan;
        _plansByCode[code] = plan;
        _featuresByPlan[id] = features;
    }
}

/// <summary>
/// Entitlement store backed by <see cref="PersonalFreePlans"/> and the in-memory seat list.
/// </summary>
internal sealed class FakeEntitlementStore : IEntitlementStore
{
    private readonly PersonalFreePlans _plans;
    private readonly IdentityStore _store;

    public FakeEntitlementStore(PersonalFreePlans plans, IdentityStore store)
    {
        _plans = plans;
        _store = store;
    }

    public Task<bool?> PlanFeatureAsync(Guid planId, string featureCode, CancellationToken cancellationToken) =>
        Task.FromResult(_plans.PlanFeatureEnabled(planId, featureCode));

    public Task<bool> RequiresSeatAsync(string featureCode, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasSeatAsync(Guid subscriptionId, Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        Task.FromResult(_store.VisibleSeats.Any(seat => seat.SubscriptionId == subscriptionId && seat.UserId == userId && seat.IsActive));
}
