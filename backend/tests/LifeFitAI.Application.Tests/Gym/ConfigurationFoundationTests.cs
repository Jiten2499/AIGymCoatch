using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class ConfigurationFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Get_CreatesDefaultsOnce_AndRejectsWhenDenied()
    {
        var configs = Substitute.For<IBusinessConfigRepository>();
        configs.GetConfigAsync(_tenantA, Arg.Any<CancellationToken>()).Returns((TenantBusinessConfig?)null, TenantBusinessConfig.CreateDefault(Guid.NewGuid(), _tenantA));
        configs.ListCategoriesAsync(_tenantA, false, Arg.Any<CancellationToken>()).Returns([], [new MemberCategoryItem(Guid.NewGuid(), "Student", null, true)]);
        configs.ListLeadSourcesAsync(_tenantA, Arg.Any<CancellationToken>()).Returns([], [new CrmCatalogItem(Guid.NewGuid(), "Walk-in", "WalkIn", true, true)]);
        configs.ListLostReasonsAsync(_tenantA, Arg.Any<CancellationToken>()).Returns([], [new CrmCatalogItem(Guid.NewGuid(), "Other", "Other", true, true)]);
        configs.ListDiscountsAsync(_tenantA, null, Arg.Any<CancellationToken>()).Returns([]);
        configs.ReadAsync(_tenantA, Arg.Any<CancellationToken>()).Returns(new BusinessConfigurationResponse(
            new MembershipConfigDto(false, true, true, 0, true),
            new FreezeConfigDto(30, 1, 2, false, 14, false),
            new AttendanceConfigDto(30, "06:00", "22:00", 15, CheckInMethod.Manual),
            new CrmConfigDto(3, 24, 2),
            [],
            [],
            [],
            []));

        var allowed = new GetBusinessConfigurationQueryHandler(Guard(null), Actor(), configs, Unit());
        (await allowed.Handle(new GetBusinessConfigurationQuery(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await configs.Received().AddConfigAsync(Arg.Any<TenantBusinessConfig>(), Arg.Any<CancellationToken>());
        await configs.Received().AddCategoryAsync(Arg.Any<MemberCategory>(), Arg.Any<CancellationToken>());

        var denied = new GetBusinessConfigurationQueryHandler(Guard(IdentityErrors.Forbidden), Actor(), configs, Unit());
        (await denied.Handle(new GetBusinessConfigurationQuery(), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
    }

    [Fact]
    public async Task MembershipAndFreezeUpdates_AreTenantScoped()
    {
        var config = TenantBusinessConfig.CreateDefault(Guid.NewGuid(), _tenantA);
        var configs = Substitute.For<IBusinessConfigRepository>();
        configs.GetConfigAsync(_tenantA, Arg.Any<CancellationToken>()).Returns(config);
        var membership = new UpdateMembershipConfigCommandHandler(Guard(null), Actor(), configs, Audit(), Unit(), Clock(), Client());
        var updated = await membership.Handle(new UpdateMembershipConfigCommand(true, false, false, 3, false), CancellationToken.None);
        updated.IsSuccess.Should().BeTrue();
        config.AllowOverlappingMemberships.Should().BeTrue();
        config.GracePeriodDays.Should().Be(3);

        var freeze = new UpdateFreezeConfigCommandHandler(Guard(null), Actor(), configs, Audit(), Unit(), Clock(), Client());
        (await freeze.Handle(new UpdateFreezeConfigCommand(20, 2, 1, true, 7, true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        config.MaxFreezeDays.Should().Be(20);
        config.FreezeRequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task DiscountRules_ValidatePercentage_PackageOwnership_AndDates()
    {
        var configs = Substitute.For<IBusinessConfigRepository>();
        var plans = Substitute.For<IMembershipRepository>();
        configs.DiscountNameExistsAsync(_tenantA, Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        plans.GetPlanAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MembershipPlan?)null);
        var create = new CreateDiscountRuleCommandHandler(Guard(null), Actor(), configs, plans, Audit(), Unit(), Clock(), Client());
        var missingPackage = await create.Handle(new CreateDiscountRuleCommand("Student", null, DiscountType.Percentage, 10, DiscountAppliesTo.SpecificPackage, Guid.NewGuid(), null, null, null, null, null, null, null), CancellationToken.None);
        missingPackage.Error.Should().Be(GymErrors.NotFound);

        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Monthly", null, 30, MembershipType.Monthly, null, 100m, "INR");
        plans.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        DiscountRule? saved = null;
        configs.AddDiscountAsync(Arg.Do<DiscountRule>(rule => saved = rule), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        configs.ListDiscountsAsync(_tenantA, null, Arg.Any<CancellationToken>()).Returns(ci =>
        [
            new DiscountRuleItem(saved!.Id, saved.Name, saved.Description, saved.DiscountType, saved.Value, saved.AppliesTo, saved.PackageId, plan.Name, saved.PackageCategory, saved.MemberCategoryId, null, saved.MinimumPackagePrice, saved.MinimumDurationDays, saved.MaximumDiscountAmount, saved.StartDate, saved.EndDate, saved.IsActive)
        ]);
        (await create.Handle(new CreateDiscountRuleCommand("Student", null, DiscountType.Percentage, 10, DiscountAppliesTo.SpecificPackage, plan.Id, null, null, null, null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None)).IsSuccess.Should().BeTrue();
        saved!.PackageId.Should().Be(plan.Id);

        var foreign = MembershipPlan.Create(Guid.NewGuid(), _tenantB, "Other", null, 30, MembershipType.Monthly, null, 10m, "USD");
        plans.GetPlanAsync(_tenantA, foreign.Id, Arg.Any<CancellationToken>()).Returns((MembershipPlan?)null);
        (await create.Handle(new CreateDiscountRuleCommand("Other", null, DiscountType.FixedAmount, 500, DiscountAppliesTo.SpecificPackage, foreign.Id, null, null, null, null, null, null, null), CancellationToken.None)).Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task MemberCategory_RejectsDuplicateNames_AndAllowsDeactivateWhenReferenced()
    {
        var configs = Substitute.For<IBusinessConfigRepository>();
        configs.CategoryNameExistsAsync(_tenantA, "Student", null, Arg.Any<CancellationToken>()).Returns(true);
        var create = new CreateMemberCategoryCommandHandler(Guard(null), Actor(), configs, Audit(), Unit(), Clock(), Client());
        (await create.Handle(new CreateMemberCategoryCommand("Student", null), CancellationToken.None)).Error.Should().Be(GymErrors.Conflict);

        var category = MemberCategory.Create(Guid.NewGuid(), _tenantA, "Women", null);
        configs.GetCategoryAsync(_tenantA, category.Id, Arg.Any<CancellationToken>()).Returns(category);
        configs.CategoryReferencedAsync(_tenantA, category.Id, Arg.Any<CancellationToken>()).Returns(true);
        var status = new ChangeMemberCategoryStatusCommandHandler(Guard(null), Actor(), configs, Audit(), Unit(), Clock(), Client());
        (await status.Handle(new ChangeMemberCategoryStatusCommand(category.Id, false), CancellationToken.None)).IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void DomainDefaults_MatchOperationalExpectations()
    {
        var config = TenantBusinessConfig.CreateDefault(Guid.NewGuid(), _tenantA);
        config.MaxFreezeDays.Should().Be(30);
        config.DefaultCheckInMethod.Should().Be(CheckInMethod.Manual);
        config.DefaultTrialDays.Should().Be(3);
        var rule = DiscountRule.Create(Guid.NewGuid(), _tenantA, "Women", null, DiscountType.Percentage, 15, DiscountAppliesTo.MemberCategory, null, null, Guid.NewGuid(), null, null, null, null, null);
        rule.PackageId.Should().BeNull();
        rule.MemberCategoryId.Should().NotBeNull();
    }

    private IOperationGuard Guard(Error? error)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(error is null ? Result.Success() : Result.Failure(error));
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userA);
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static IAuditLogRepository Audit()
    {
        var audit = Substitute.For<IAuditLogRepository>();
        audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return audit;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return unit;
    }

    private static IClientContext Client() => Substitute.For<IClientContext>();
}
