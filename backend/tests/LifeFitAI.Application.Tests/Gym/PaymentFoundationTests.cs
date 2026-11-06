using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class PaymentFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private readonly DateOnly _today = new(2026, 10, 7);

    [Fact]
    public void Pricing_AppliesFixedDiscountAndSettlement()
    {
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Premium", null, 365, MembershipType.Yearly, null, 12000m, "INR");
        var rule = DiscountRule.Create(Guid.NewGuid(), _tenantA, "Festive", null, DiscountType.FixedAmount, 1000m, DiscountAppliesTo.AllPackages, null, null, null, null, null, null, null, null);
        var snapshot = MembershipPricing.Snapshot(plan.Price, rule, _today);
        snapshot.DiscountAmount.Should().Be(1000m);
        snapshot.FinalAmount.Should().Be(11000m);
        MembershipPaymentRules.Settlement(11000m, 0m).Should().Be(MembershipSettlementStatus.Unpaid);
        MembershipPaymentRules.Settlement(11000m, 6000m).Should().Be(MembershipSettlementStatus.PartiallyPaid);
        MembershipPaymentRules.Settlement(11000m, 11000m).Should().Be(MembershipSettlementStatus.Paid);
        MembershipPaymentRules.Outstanding(11000m, 6000m).Should().Be(5000m);
    }

    [Fact]
    public void Payment_RequiresReferenceForUpi()
    {
        var act = () => MembershipPayment.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), 100m, "INR", MembershipPaymentMethod.Upi, _today, _userId, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task CreateMembership_WithoutPayment_LeavesFullOutstanding()
    {
        var memberId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan = MembershipPlan.Create(planId, _tenantA, "Premium", null, 365, MembershipType.Yearly, null, 12000m, "INR");
        var member = MemberProfile.Create(memberId, _tenantA, _userId, "M1", _today);
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, memberId, Arg.Any<CancellationToken>()).Returns(member);
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.GetPlanAsync(_tenantA, planId, Arg.Any<CancellationToken>()).Returns(plan);
        memberships.OverlapsMembershipAsync(_tenantA, memberId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns(false);
        GymMembership? saved = null;
        memberships.When(x => x.AddMembershipAsync(Arg.Any<GymMembership>(), Arg.Any<CancellationToken>()))
            .Do(call => saved = call.ArgAt<GymMembership>(0));
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(CancellationToken.None));
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var handler = CreateMembershipHandler(people, memberships, Substitute.For<IMembershipPaymentRepository>(), Substitute.For<IBusinessConfigRepository>(), unit);
        var result = await handler.Handle(new CreateMembershipCommand(memberId, null, planId, _today, false, null), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        saved!.FinalAmount.Should().Be(12000m);
        result.Value.TotalPaid.Should().Be(0m);
        result.Value.Outstanding.Should().Be(12000m);
        result.Value.SettlementStatus.Should().Be(MembershipSettlementStatus.Unpaid);
    }

    [Fact]
    public async Task RecordPayment_RejectsOverpaymentAndCrossTenant()
    {
        var membership = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), null, Guid.NewGuid(), "G1", _today, _today.AddDays(30), _today, false, null);
        membership.SetPricing(10000m, 0m, 10000m, "INR", null, null);
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.GetMembershipAsync(_tenantA, membership.Id, Arg.Any<CancellationToken>()).Returns(membership);
        memberships.GetMembershipAsync(_tenantB, membership.Id, Arg.Any<CancellationToken>()).Returns((GymMembership?)null);
        var payments = Substitute.For<IMembershipPaymentRepository>();
        payments.SumPostedAsync(_tenantA, membership.Id, Arg.Any<CancellationToken>()).Returns(8000m);
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantA, Arg.Any<CancellationToken>()).Returns((BusinessProfile?)null);
        var handler = PaymentHandler(memberships, payments, profiles);

        var over = await handler.Handle(new RecordMembershipPaymentCommand(membership.Id, 3000m, MembershipPaymentMethod.Cash, _today, null, null), CancellationToken.None);
        over.Error.Should().Be(GymErrors.InvalidState);

        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantB);
        actor.UserId.Returns(_userId);
        var foreign = new PaymentCommandHandler(Guard(null), actor, memberships, payments, NoopLedger(), profiles, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var cross = await foreign.Handle(new RecordMembershipPaymentCommand(membership.Id, 1000m, MembershipPaymentMethod.Cash, _today, null, null), CancellationToken.None);
        cross.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task RecordPayment_PartialThenFull()
    {
        var membership = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), null, Guid.NewGuid(), "G2", _today, _today.AddDays(30), _today, false, null);
        membership.SetPricing(12000m, 1000m, 11000m, "INR", null, null);
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.GetMembershipAsync(_tenantA, membership.Id, Arg.Any<CancellationToken>()).Returns(membership);
        var payments = Substitute.For<IMembershipPaymentRepository>();
        payments.SumPostedAsync(_tenantA, membership.Id, Arg.Any<CancellationToken>()).Returns(0m, 6000m);
        payments.ReadAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new MembershipPaymentItem(call.ArgAt<Guid>(1), membership.MemberProfileId, membership.Id, 6000m, "INR", MembershipPaymentMethod.Upi, _today, "UPI1", null, MembershipPaymentStatus.Posted, _userId, "Ada", _utc, null));
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantA, Arg.Any<CancellationToken>()).Returns((BusinessProfile?)null);
        var handler = PaymentHandler(memberships, payments, profiles);

        var first = await handler.Handle(new RecordMembershipPaymentCommand(membership.Id, 6000m, MembershipPaymentMethod.Upi, _today, "UPI1", null), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        await payments.Received(1).AddAsync(Arg.Is<MembershipPayment>(p => p.Amount == 6000m && p.Status == MembershipPaymentStatus.Posted), Arg.Any<CancellationToken>());

        var second = await handler.Handle(new RecordMembershipPaymentCommand(membership.Id, 5000m, MembershipPaymentMethod.Cash, _today, null, null), CancellationToken.None);
        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Void_RequiresPermissionSeparateFromCreate()
    {
        var payment = MembershipPayment.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), 1000m, "INR", MembershipPaymentMethod.Cash, _today, _userId, null, null);
        var payments = Substitute.For<IMembershipPaymentRepository>();
        payments.GetAsync(_tenantA, payment.Id, Arg.Any<CancellationToken>()).Returns(payment);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.PaymentVoid, FeatureCodes.GymPaymentManagement, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(GymErrors.FeatureDisabled));
        var handler = new PaymentCommandHandler(guard, Actor(), Substitute.For<IMembershipRepository>(), payments, NoopLedger(), Substitute.For<IBusinessProfileRepository>(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new VoidMembershipPaymentCommand(payment.Id), CancellationToken.None);
        result.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    [Fact]
    public async Task FeatureDisabled_BlocksCollectPayment()
    {
        var handler = new PaymentCommandHandler(Guard(GymErrors.FeatureDisabled), Actor(), Substitute.For<IMembershipRepository>(), Substitute.For<IMembershipPaymentRepository>(), NoopLedger(), Substitute.For<IBusinessProfileRepository>(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new RecordMembershipPaymentCommand(Guid.NewGuid(), 100m, MembershipPaymentMethod.Cash, _today, null, null), CancellationToken.None);
        result.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    private CreateMembershipCommandHandler CreateMembershipHandler(
        IPeopleRepository people,
        IMembershipRepository memberships,
        IMembershipPaymentRepository payments,
        IBusinessConfigRepository configs,
        IUnitOfWork unit) =>
        new(
            Guard(null),
            Actor(),
            Substitute.For<IPermissionService>(),
            people,
            Substitute.For<IBranchRepository>(),
            memberships,
            payments,
            NoopLedger(),
            configs,
            Substitute.For<IMemberAppSeatAssigner>(),
            Substitute.For<IAuditLogRepository>(),
            unit,
            Clock(),
            Substitute.For<IClientContext>());

    private PaymentCommandHandler PaymentHandler(IMembershipRepository memberships, IMembershipPaymentRepository payments, IBusinessProfileRepository profiles) =>
        new(Guard(null), Actor(), memberships, payments, NoopLedger(), profiles, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());

    private static ILedgerPostingService NoopLedger()
    {
        var ledger = Substitute.For<ILedgerPostingService>();
        ledger.TryPostMembershipPaymentAsync(Arg.Any<Guid>(), Arg.Any<MembershipPayment>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        ledger.TryReverseMembershipPaymentAsync(Arg.Any<Guid>(), Arg.Any<MembershipPayment>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return ledger;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantA);
        actor.UserId.Returns(_userId);
        return actor;
    }

    private static IOperationGuard Guard(Error? error)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(error is null ? Result.Success() : Result.Failure(error));
        return guard;
    }

    private IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_utc);
        return clock;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return unit;
    }
}
