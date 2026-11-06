using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MembershipRenewalHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _utc = new(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Renew_UsesTenantLocalTodayAndDoesNotCopyTheOldBalance()
    {
        var current = Current(new DateOnly(2026, 8, 1), 10);
        current.SetPricing(200m, 0m, 200m, "USD", null, null);
        var plan = Plan(8);
        var harness = CreateHarness(current, plan);
        var rule = DiscountRule.Create(Guid.NewGuid(), _tenantId, "Ten", null, DiscountType.Percentage, 10m, DiscountAppliesTo.AllPackages, null, null, null, null, null, null, null, null);
        harness.Configs.GetDiscountAsync(_tenantId, rule.Id, Arg.Any<CancellationToken>()).Returns(rule);

        var tooSoon = await harness.Handler.Handle(Command(current.Id, plan.Id, new DateOnly(2026, 9, 30), rule.Id, null, false, null, null), CancellationToken.None);
        var result = await harness.Handler.Handle(Command(current.Id, plan.Id, new DateOnly(2026, 10, 1), rule.Id, null, false, null, new InitialMembershipPayment(40m, MembershipPaymentMethod.Cash, new DateOnly(2026, 10, 1), null, null)), CancellationToken.None);

        tooSoon.Error.Code.Should().Be("gym.invalid_state");

        result.IsSuccess.Should().BeTrue();
        result.Value.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        result.Value.FinalAmount.Should().Be(90m);
        result.Value.DiscountAmount.Should().Be(10m);
        result.Value.TotalPaid.Should().Be(40m);
        result.Value.Outstanding.Should().Be(50m);
        result.Value.SettlementStatus.Should().Be(MembershipSettlementStatus.PartiallyPaid);
        current.FinalAmount.Should().Be(200m);
        harness.Saved!.IncludedPtSessions.Should().Be(8);
        harness.Saved.PtSessionsRemaining.Should().Be(8);
        await harness.Memberships.Received().GetMembershipAsync(_tenantId, current.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renew_RejectsInactivePlansCancelledMembershipsAndOverlaps()
    {
        var current = Current(new DateOnly(2026, 10, 1), 31);
        var inactive = Plan(0);
        inactive.Deactivate();
        var inactiveRenewalHarness = CreateHarness(current, inactive);
        (await inactiveRenewalHarness.Handler.Handle(Command(current.Id, inactive.Id, new DateOnly(2026, 11, 1), null, null, false, null, null), CancellationToken.None)).Error.Code.Should().Be("gym.invalid_state");

        current.Cancel();
        var cancelled = CreateHarness(current, Plan(0));
        (await cancelled.Handler.Handle(Command(current.Id, cancelled.Plan.Id, null, null, null, false, null, null), CancellationToken.None)).Error.Code.Should().Be("gym.invalid_state");

        var active = Current(new DateOnly(2026, 10, 1), 31);
        var overlap = CreateHarness(active, Plan(0));
        overlap.Memberships.OverlapsMembershipAsync(_tenantId, active.MemberProfileId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), active.Id, Arg.Any<CancellationToken>()).Returns(true);
        (await overlap.Handler.Handle(Command(active.Id, overlap.Plan.Id, new DateOnly(2026, 11, 1), null, null, false, null, null), CancellationToken.None)).Error.Code.Should().Be("gym.conflict");
        await overlap.Memberships.DidNotReceive().AddMembershipAsync(Arg.Any<GymMembership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renew_KeepsTheSameTrainerAndRejectsASecondActivePtPackage()
    {
        var current = Current(new DateOnly(2026, 8, 1), 10);
        var trainer = TrainerProfile.Create(Guid.NewGuid(), _tenantId, Guid.NewGuid(), null);
        var package = PtPackage.Create(Guid.NewGuid(), _tenantId, "PT", null, 6, 30, 50m, "USD");
        var harness = CreateHarness(current, Plan(0));
        harness.People.GetTrainerAsync(_tenantId, trainer.Id, Arg.Any<CancellationToken>()).Returns(trainer);
        harness.Packages.GetAsync(_tenantId, package.Id, Arg.Any<CancellationToken>()).Returns(package);
        harness.Packages.HasActiveAssignmentAsync(_tenantId, current.MemberProfileId, package.Id, Arg.Any<CancellationToken>()).Returns(false, true);

        var first = await harness.Handler.Handle(Command(current.Id, harness.Plan.Id, new DateOnly(2026, 10, 1), null, trainer.Id, false, package.Id, null), CancellationToken.None);
        var second = await harness.Handler.Handle(Command(current.Id, harness.Plan.Id, new DateOnly(2026, 10, 1), null, trainer.Id, false, package.Id, null), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Error.Code.Should().Be("gym.conflict");
        await harness.Trainers.Received().SyncMemberTrainerAsync(_tenantId, current.MemberProfileId, trainer.Id, new DateOnly(2026, 10, 1), _userId, Arg.Any<CancellationToken>());
        await harness.Packages.Received(1).AddAssignmentAsync(Arg.Is<MemberPtAssignment>(item => item.SessionsRemaining == 6 && item.PaymentStatus == PtPaymentStatus.Unpaid), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renew_SkipsTrainerSyncWhenTheCoachIsUnchangedAndBlocksEarlyRenewalWhenDisabled()
    {
        var current = Current(new DateOnly(2026, 10, 1), 31);
        var harness = CreateHarness(current, Plan(0));
        var config = TenantBusinessConfig.CreateDefault(Guid.NewGuid(), _tenantId);
        config.UpdateMembership(false, false, true, 0, true);
        harness.Configs.GetConfigAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(config);

        var blocked = await harness.Handler.Handle(Command(current.Id, harness.Plan.Id, new DateOnly(2026, 11, 1), null, null, false, null, null), CancellationToken.None);
        harness.Configs.GetConfigAsync(_tenantId, Arg.Any<CancellationToken>()).Returns((TenantBusinessConfig?)null);
        var kept = await harness.Handler.Handle(Command(current.Id, harness.Plan.Id, new DateOnly(2026, 11, 1), null, null, false, null, null), CancellationToken.None);

        blocked.Error.Code.Should().Be("gym.invalid_state");
        kept.IsSuccess.Should().BeTrue();
        await harness.Trainers.DidNotReceive().SyncMemberTrainerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateOnly>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private GymMembership Current(DateOnly start, int days) =>
        GymMembership.Create(Guid.NewGuid(), _tenantId, Guid.NewGuid(), null, Guid.NewGuid(), "GCURRENT1", start, days, new DateOnly(2026, 10, 1), false, null);

    private MembershipPlan Plan(int sessions) =>
        MembershipPlan.Create(Guid.NewGuid(), _tenantId, "Plan", null, 30, MembershipType.Monthly, null, 100m, "USD", includedPtSessions: sessions);

    private RenewMembershipCommand Command(Guid membershipId, Guid? planId, DateOnly? start, Guid? discountId, Guid? trainerId, bool clearTrainer, Guid? packageId, InitialMembershipPayment? payment) =>
        new(membershipId, null, false, planId, start, trainerId, clearTrainer, packageId, discountId, payment);

    private RenewalHarness CreateHarness(GymMembership current, MembershipPlan plan)
    {
        var memberships = Substitute.For<IMembershipRepository>();
        var profiles = Substitute.For<IBusinessProfileRepository>();
        var people = Substitute.For<IPeopleRepository>();
        var trainers = Substitute.For<ITrainerWorkspace>();
        var packages = Substitute.For<IPtPackageRepository>();
        var payments = Substitute.For<IMembershipPaymentRepository>();
        var ledger = Substitute.For<ILedgerPostingService>();
        var unit = Substitute.For<IUnitOfWork>();
        var configs = Substitute.For<IBusinessConfigRepository>();
        var box = new SavedMembership();
        memberships.GetMembershipAsync(_tenantId, current.Id, Arg.Any<CancellationToken>()).Returns(current);
        memberships.GetPlanAsync(_tenantId, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        memberships.AddMembershipAsync(Arg.Do<GymMembership>(item => box.Value = item), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        profiles.GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), _tenantId, "Asia/Kolkata", "INR"));
        ledger.TryPostMembershipPaymentAsync(_tenantId, Arg.Any<MembershipPayment>(), _userId, Arg.Any<CancellationToken>()).Returns(Result.Success());
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantId);
        actor.UserId.Returns(_userId);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var handler = new RenewMembershipCommandHandler(guard, actor, memberships, configs, profiles, people, trainers, packages, payments, ledger, Substitute.For<IAuditLogRepository>(), unit, clock, Substitute.For<IClientContext>());
        return new RenewalHarness(handler, memberships, configs, people, trainers, packages, box, plan);
    }

    private sealed class SavedMembership
    {
        public GymMembership? Value { get; set; }
    }

    private sealed class RenewalHarness
    {
        public RenewalHarness(RenewMembershipCommandHandler handler, IMembershipRepository memberships, IBusinessConfigRepository configs, IPeopleRepository people, ITrainerWorkspace trainers, IPtPackageRepository packages, SavedMembership saved, MembershipPlan plan)
        {
            Handler = handler;
            Memberships = memberships;
            Configs = configs;
            People = people;
            Trainers = trainers;
            Packages = packages;
            SavedBox = saved;
            Plan = plan;
        }

        public RenewMembershipCommandHandler Handler { get; }

        public IMembershipRepository Memberships { get; }

        public IBusinessConfigRepository Configs { get; }

        public IPeopleRepository People { get; }

        public ITrainerWorkspace Trainers { get; }

        public IPtPackageRepository Packages { get; }

        public SavedMembership SavedBox { get; }

        public MembershipPlan Plan { get; }

        public GymMembership? Saved => SavedBox.Value;
    }
}
