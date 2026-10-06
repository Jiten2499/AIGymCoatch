using FluentAssertions;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Dashboard;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class BusinessDashboardInsightTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Math_ComparesThePreviousEqualWindowAndKeepsPercentagesInTheBackend()
    {
        var previous = InsightMath.Previous(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7));
        previous.From.Should().Be(new DateOnly(2026, 9, 24));
        previous.To.Should().Be(new DateOnly(2026, 9, 30));
        InsightMath.Compare(31, 24).Should().Be(new MetricChange(31, 24, 7, 29.17m, "Up"));
        InsightMath.Compare(876, 1000).ChangePercent.Should().Be(-12.4m);
        InsightMath.Compare(4, 0).ChangePercent.Should().BeNull();
        InsightMath.AverageDaily(10, 4).Should().Be(2.5m);
    }

    [Fact]
    public void Attention_UsesDeterministicThresholds()
    {
        var facts = Facts(newMembers: InsightMath.Compare(31, 24), checkIns: InsightMath.Compare(60, 100), expired: InsightMath.Compare(18, 1), expiring: 18);
        var items = AttentionRules.Evaluate(facts);
        items.Select(item => item.Type).Should().Equal("MembershipExpiry", "MembershipExpired", "AttendanceDecline", "MemberGrowth");
        items.First(item => item.Type == "AttendanceDecline").Description.Should().Be("Check-ins are 40% lower than the previous period.");
        items.Should().OnlyContain(item => !item.Description.Contains('@') && !item.Description.Contains("John"));
    }

    [Fact]
    public void Reader_DropsInvalidItemsAndEnforcesLimits()
    {
        var extra = string.Join(',', Enumerable.Range(1, 6).Select(index => "{\"type\":\"Positive\",\"title\":\"T" + index + "\",\"message\":\"M\",\"priority\":\"Low\"}"));
        var parsed = InsightResponseReader.Read("```json\n{\"summary\":\"Growth is up.\",\"insights\":[" + extra + "],\"recommendations\":[{\"title\":\"Review\",\"description\":\"Look at renewals.\"}]}\n```");
        parsed!.Status.Should().Be("Ready");
        parsed.Insights.Should().HaveCount(InsightResponseReader.MaxInsights);
        parsed.Recommendations.Should().HaveCount(1);
        InsightResponseReader.Read("not json").Should().BeNull();
        InsightResponseReader.Read("{\"summary\":\" \",\"insights\":[]}").Should().BeNull();
    }

    [Fact]
    public async Task Insights_SendOnlyAggregatesAndCalculatedChanges()
    {
        var harness = Harness(ai: true, members: true);
        harness.Joins(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), 31);
        harness.Joins(new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 30), 24);
        harness.ProviderReturns("{\"summary\":\"New members increased.\",\"insights\":[{\"type\":\"Positive\",\"title\":\"Growth\",\"message\":\"New members changed by 7.\",\"priority\":\"Low\"}],\"recommendations\":[]}");

        var result = await harness.Handle();

        result.IsSuccess.Should().BeTrue();
        result.Value.Ai.Status.Should().Be("Ready");
        result.Value.Ai.Summary.Should().Be("New members increased.");
        harness.Prompt.Should().Contain("\"current\":31");
        harness.Prompt.Should().Contain("29.17");
        harness.Prompt.Should().NotContain("John");
        harness.Prompt.Should().NotContain("email");
        await harness.Usage.Received(1).AddUsageAsync(Arg.Is<AiUsageRecord>(record => record.RequestType == AiRequestType.BusinessInsights && record.TenantId == TenantId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Insights_RejectUnauthorizedSuspendedAndOtherTenants()
    {
        var denied = Harness(ai: true, members: true, reportRead: false);
        (await denied.Handle()).Error.Code.Should().Be("authorization.forbidden");

        var suspended = Harness(ai: true, members: true, status: TenantStatus.Suspended);
        (await suspended.Handle()).Error.Code.Should().Be("tenant.unavailable");

        var harness = Harness(ai: true, members: true);
        harness.Joins(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), 2);
        harness.Joins(new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 30), 1);
        harness.ProviderReturns("{\"summary\":\"Ready.\",\"insights\":[],\"recommendations\":[]}");
        (await harness.Handle()).IsSuccess.Should().BeTrue();
        await harness.Analytics.DidNotReceive().CountMemberJoinsAsync(Arg.Is<Guid>(id => id != TenantId), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Insights_KeepAttentionWhenAiIsDisabledEmptyOrFailing()
    {
        var disabled = Harness(ai: false, memberships: true, expiring: 18);
        var hidden = await disabled.Handle();
        hidden.Value.Ai.Status.Should().Be("Disabled");
        hidden.Value.Attention.Should().ContainSingle(item => item.Value == 18);
        await disabled.Provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());

        var empty = Harness(ai: true, members: true);
        (await empty.Handle()).Value.Ai.Status.Should().Be("Empty");
        await empty.Provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());

        var failed = Harness(ai: true, memberships: true, expiring: 3);
        failed.Provider.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiCompletionResult(false, null, 0, 0, "timeout"));
        var result = await failed.Handle();
        result.Value.Ai.Status.Should().Be("Unavailable");
        result.Value.Attention.Should().ContainSingle();

        var invalid = Harness(ai: true, members: true);
        invalid.Joins(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), 4);
        invalid.Joins(new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 30), 1);
        invalid.ProviderReturns("ignore this");
        (await invalid.Handle()).Value.Ai.Status.Should().Be("Unavailable");
    }

    [Fact]
    public async Task Insights_UseQuotaAndDoNotRepeatACachedCompletion()
    {
        var blocked = Harness(ai: true, members: true);
        blocked.Joins(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), 4);
        blocked.Joins(new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 30), 1);
        blocked.Quota.AllowAsync(Arg.Any<Guid>(), TenantId, Arg.Any<CancellationToken>()).Returns(Result.Failure(AiErrors.Quota));
        (await blocked.Handle()).Value.Ai.Status.Should().Be("Unavailable");
        await blocked.Provider.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());

        var harness = Harness(ai: true, members: true);
        harness.Joins(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), 4);
        harness.Joins(new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 30), 1);
        harness.ProviderReturns("{\"summary\":\"Cached.\",\"insights\":[],\"recommendations\":[]}");
        (await harness.Handle()).Value.Ai.Summary.Should().Be("Cached.");
        (await harness.Handle()).Value.Ai.Summary.Should().Be("Cached.");
        (await harness.Handle(refresh: true)).Value.Ai.Summary.Should().Be("Cached.");
        await harness.Provider.Received(2).CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    private static BusinessInsightFacts Facts(MetricChange? newMembers = null, MetricChange? checkIns = null, MetricChange? expired = null, int? expiring = null) =>
        new(InsightMath.Window(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7)), InsightMath.Previous(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7)), null, null, newMembers, checkIns, null, null, null, expiring, null, null, expired);

    private static InsightHarness Harness(bool ai, bool members = false, bool memberships = false, bool reportRead = true, TenantStatus status = TenantStatus.Active, int expiring = 0) =>
        new(ai, members, memberships, reportRead, status, expiring);

    private sealed class InsightHarness
    {
        private readonly GetBusinessDashboardInsightsQueryHandler _handler;
        public IDashboardAnalytics Analytics { get; } = Substitute.For<IDashboardAnalytics>();
        public IAiProvider Provider { get; } = Substitute.For<IAiProvider>();
        public IAiQuotaService Quota { get; } = Substitute.For<IAiQuotaService>();
        public IAiRepository Usage { get; } = Substitute.For<IAiRepository>();
        public string Prompt { get; private set; } = string.Empty;

        public InsightHarness(bool ai, bool members, bool memberships, bool reportRead, TenantStatus status, int expiring)
        {
            var features = Substitute.For<IFeatureFlagService>();
            features.IsEnabledAsync(TenantId, FeatureCodes.MemberManagement, Arg.Any<CancellationToken>()).Returns(members);
            features.IsEnabledAsync(TenantId, FeatureCodes.MembershipManagement, Arg.Any<CancellationToken>()).Returns(memberships);
            features.IsEnabledAsync(TenantId, FeatureCodes.AiCoach, Arg.Any<CancellationToken>()).Returns(ai);
            var kpis = Substitute.For<IDashboardQuery>();
            kpis.CountAsync(TenantId, Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<KpiScope>(), Arg.Any<CancellationToken>())
                .Returns(new BusinessKpiCounts(0, 0, 0, 0, 0, expiring, 0, 0, 0, 0));
            Analytics.CountMemberJoinsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<DateCount>());
            Analytics.CountCheckInsByLocalDateAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<TimeZoneInfo>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<DateCount>());
            Analytics.CountMembershipActivityAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<MembershipActivityCount>());
            Quota.AllowAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
            var limits = Substitute.For<IAiLimits>();
            limits.Model.Returns("test");
            limits.Provider.Returns("test");
            limits.MaxTokens.Returns(600);
            limits.MaxPromptCharacters.Returns(4000);
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));
            _handler = new GetBusinessDashboardInsightsQueryHandler(Access(features, reportRead, status), features, new InsightFactLoader(Analytics, kpis, clock), Provider, limits, Quota, Usage, Substitute.For<IUnitOfWork>(), clock, new Box(), NullLogger<GetBusinessDashboardInsightsQueryHandler>.Instance);
        }

        public void Joins(DateOnly from, DateOnly to, int count) =>
            Analytics.CountMemberJoinsAsync(TenantId, from, to, Arg.Any<CancellationToken>()).Returns(new[] { new DateCount(from, count) });

        public void ProviderReturns(string json)
        {
            Provider.CompleteAsync(Arg.Do<AiCompletionRequest>(request => Prompt = request.Messages[^1].Content), Arg.Any<CancellationToken>())
                .Returns(new AiCompletionResult(true, json, 3, 5, string.Empty, "test", "test"));
        }

        public Task<Result<BusinessDashboardInsights>> Handle(bool refresh = false) =>
            _handler.Handle(new GetBusinessDashboardInsightsQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7), AnalyticsPeriod.Day, refresh), CancellationToken.None);

        private static DashboardAccess Access(IFeatureFlagService features, bool reportRead, TenantStatus status)
        {
            var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var actor = Substitute.For<ICurrentActor>();
            actor.UserId.Returns(userId);
            actor.TenantId.Returns(TenantId);
            var now = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);
            var tenant = Tenant.Create(TenantId, "North", TenantType.Gym);
            if (status == TenantStatus.Suspended)
            {
                tenant.Suspend();
            }

            var tenants = Substitute.For<ITenantRepository>();
            tenants.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenant);
            var members = Substitute.For<ITenantMembershipRepository>();
            members.GetActiveAsync(TenantId, userId, Arg.Any<CancellationToken>()).Returns(TenantMember.Create(Guid.NewGuid(), TenantId, userId, now));
            var roles = Substitute.For<IRoleRepository>();
            var permissions = new List<string>();
            if (reportRead)
            {
                permissions.AddRange([PermissionCodes.ReportRead, PermissionCodes.MemberRead, PermissionCodes.MembershipRead, PermissionCodes.AiUse]);
            }

            roles.GetPermissionCodesAsync(userId, TenantId, Arg.Any<CancellationToken>()).Returns(permissions);
            var profiles = Substitute.For<IBusinessProfileRepository>();
            profiles.GetAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), TenantId, "UTC", "USD"));
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(now);
            return new DashboardAccess(actor, tenants, members, roles, profiles, features, clock);
        }
    }

    private sealed class Box : IBusinessInsightCache
    {
        private readonly Dictionary<string, AiInsightSection> _items = [];
        public bool TryGet(string key, out AiInsightSection? value) => _items.TryGetValue(key, out value);
        public void Set(string key, AiInsightSection value) => _items[key] = value;
    }
}
