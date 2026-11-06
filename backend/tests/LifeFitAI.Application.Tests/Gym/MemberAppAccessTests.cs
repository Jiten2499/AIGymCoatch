using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MemberAppAccessTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _actorId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
    private readonly DateOnly _today = new(2026, 10, 6);

    [Fact]
    public async Task Membership_CanBeCreatedWithoutAppAccess()
    {
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), "M1", _today);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Annual", null, 365, MembershipType.Yearly, null, 15000m, "INR");
        var people = Substitute.For<IPeopleRepository>();
        var memberships = Substitute.For<IMembershipRepository>();
        var seats = Substitute.For<IMemberAppSeatAssigner>();
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        memberships.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        memberships.OverlapsMembershipAsync(_tenantA, member.Id, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns(false);

        var result = await MembershipHandler(people, memberships, seats)
            .Handle(new CreateMembershipCommand(member.Id, null, plan.Id, _today, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await seats.DidNotReceive().AssignAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<MemberProfile>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membership_WithAppAccess_ConsumesOneSeat()
    {
        var userId = Guid.NewGuid();
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, userId, "M2", _today);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Annual", null, 365, MembershipType.Yearly, null, 15000m, "INR");
        var subscriptionId = Guid.NewGuid();
        var people = Substitute.For<IPeopleRepository>();
        var memberships = Substitute.For<IMembershipRepository>();
        var seats = Substitute.For<IMemberAppSeatAssigner>();
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        memberships.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        memberships.OverlapsMembershipAsync(_tenantA, member.Id, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>()).Returns(false);
        seats.AssignAsync(_tenantA, _actorId, member, subscriptionId, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MemberAppAccessResponse(member.Id, userId, true, true, "Active", "Active", Guid.NewGuid(), subscriptionId, "PREMIUM", "LifeFit Premium", _utc, 19)));

        var result = await MembershipHandler(people, memberships, seats)
            .Handle(new CreateMembershipCommand(member.Id, null, plan.Id, _today, false, null, true, subscriptionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await seats.Received(1).AssignAsync(_tenantA, _actorId, member, subscriptionId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppAccess_FailsWhenNoSeatsRemain()
    {
        var userId = Guid.NewGuid();
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, userId, "M3", _today);
        var subscriptionId = Guid.NewGuid();
        var people = Substitute.For<IPeopleRepository>();
        var seats = Substitute.For<IMemberAppSeatAssigner>();
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        seats.AssignAsync(_tenantA, _actorId, member, subscriptionId, null, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MemberAppAccessResponse>(new Error("license.capacity_conflict", "No seats are available.")));

        var result = await AssignHandler(people, seats, licenseManage: true)
            .Handle(new AssignMemberAppPlanCommand(member.Id, subscriptionId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("license.capacity_conflict");
    }

    [Fact]
    public async Task BasicAccess_DoesNotConsumeASeat()
    {
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, null, "M-BASIC", _today);
        var user = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "rahul@gym.test", null, "hash", "salt", "Rahul", "Sharma");
        var accounts = Substitute.For<IAccountLinker>();
        accounts.ResolveAsync("rahul@gym.test", Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        var roles = Substitute.For<IRoleRepository>();
        roles.GetByNameAsync(RoleNames.Member, Arg.Any<CancellationToken>()).Returns(Role.Create(Guid.NewGuid(), RoleNames.Member, "Member"));
        roles.GetRoleNamesAsync(user.Id, _tenantA, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberByUserAsync(_tenantA, user.Id, Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var store = Substitute.For<ILicenseAdminStore>();
        store.ActiveSeatsForUserAsync(_tenantA, user.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<SubscriptionSeat>());
        var assigner = new MemberAppSeatAssigner(
            accounts,
            Substitute.For<ITenantMembershipRepository>(),
            roles,
            people,
            store,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IPermissionCacheInvalidator>(),
            Clock(),
            Substitute.For<IClientContext>());

        var result = await assigner.EnableBasicAccessAsync(_tenantA, _actorId, member, "rahul@gym.test", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasBasicAccess.Should().BeTrue();
        result.Value.HasAppPlan.Should().BeFalse();
        result.Value.HasAppAccess.Should().BeTrue();
        await store.DidNotReceive().ReserveSeatAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppAccess_RejectsUnlicensedSubscription()
    {
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), "M4", _today);
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        var assigner = new MemberAppSeatAssigner(
            Substitute.For<IAccountLinker>(),
            Substitute.For<ITenantMembershipRepository>(),
            Substitute.For<IRoleRepository>(),
            people,
            store,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IPermissionCacheInvalidator>(),
            Clock(),
            Substitute.For<IClientContext>());

        var result = await assigner.AssignAsync(_tenantA, _actorId, member, Guid.NewGuid(), null, CancellationToken.None);

        result.Error.Code.Should().Be("license.not_found");
    }

    [Fact]
    public async Task DuplicateAssignment_ReturnsExistingSeat()
    {
        var userId = Guid.NewGuid();
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, userId, "M5", _today);
        var subscription = Subscription.ForTenant(Guid.NewGuid(), _tenantA, Guid.NewGuid(), _utc, 50);
        var existing = SubscriptionSeat.Create(Guid.NewGuid(), subscription.Id, userId, _actorId, _utc);
        var store = Substitute.For<ILicenseAdminStore>();
        store.GetTenantSubscriptionAsync(_tenantA, subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.ActiveSeatsForUserAsync(_tenantA, userId, Arg.Any<CancellationToken>()).Returns(new[] { existing });
        store.GetSubscriptionAsync(subscription.Id, Arg.Any<CancellationToken>()).Returns(subscription);
        store.PlanCodeAsync(subscription.PlanId, Arg.Any<CancellationToken>()).Returns("PREMIUM");
        store.GetPlanAsync(subscription.PlanId, Arg.Any<CancellationToken>()).Returns(PlanDefinition.Create(subscription.PlanId, "PREMIUM", "LifeFit Premium", null, "Business", 1, 50));
        store.ActiveSeatCountAsync(subscription.Id, Arg.Any<CancellationToken>()).Returns(31);
        var assigner = new MemberAppSeatAssigner(
            Substitute.For<IAccountLinker>(),
            Substitute.For<ITenantMembershipRepository>(),
            Substitute.For<IRoleRepository>(),
            Substitute.For<IPeopleRepository>(),
            store,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IPermissionCacheInvalidator>(),
            Clock(),
            Substitute.For<IClientContext>());

        var result = await assigner.AssignAsync(_tenantA, _actorId, member, subscription.Id, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasBasicAccess.Should().BeTrue();
        result.Value.HasAppPlan.Should().BeTrue();
        result.Value.SeatId.Should().Be(existing.Id);
        await store.DidNotReceive().ReserveSeatAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAppAccess_ReleasesSeat()
    {
        var userId = Guid.NewGuid();
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, userId, "M6", _today);
        var seat = SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), userId, _actorId, _utc);
        var store = Substitute.For<ILicenseAdminStore>();
        store.ActiveSeatsForUserAsync(_tenantA, userId, Arg.Any<CancellationToken>()).Returns(new[] { seat });
        var assigner = new MemberAppSeatAssigner(
            Substitute.For<IAccountLinker>(),
            Substitute.For<ITenantMembershipRepository>(),
            Substitute.For<IRoleRepository>(),
            Substitute.For<IPeopleRepository>(),
            store,
            Substitute.For<IAuditLogRepository>(),
            Substitute.For<IPermissionCacheInvalidator>(),
            Clock(),
            Substitute.For<IClientContext>());

        var result = await assigner.ReleaseAsync(_tenantA, _actorId, member, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        seat.Status.Should().Be(SeatStatus.Released);
    }

    [Fact]
    public async Task CrossTenantMember_CannotBeAssigned()
    {
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);

        var result = await AssignHandler(people, Substitute.For<IMemberAppSeatAssigner>(), licenseManage: true)
            .Handle(new AssignMemberAppPlanCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task UnauthorizedStaff_CannotAssignAppAccess()
    {
        var people = Substitute.For<IPeopleRepository>();
        var result = await AssignHandler(people, Substitute.For<IMemberAppSeatAssigner>(), licenseManage: false)
            .Handle(new AssignMemberAppPlanCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(IdentityErrors.Forbidden.Code);
        await people.DidNotReceive().GetMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MembershipWithAppAccess_RequiresLinkedLogin()
    {
        var member = MemberProfile.Create(Guid.NewGuid(), _tenantA, null, "M7", _today);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Annual", null, 365, MembershipType.Yearly, null, 15000m, "INR");
        var people = Substitute.For<IPeopleRepository>();
        var memberships = Substitute.For<IMembershipRepository>();
        people.GetMemberAsync(_tenantA, member.Id, Arg.Any<CancellationToken>()).Returns(member);
        memberships.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await MembershipHandler(people, memberships, Substitute.For<IMemberAppSeatAssigner>())
            .Handle(new CreateMembershipCommand(member.Id, null, plan.Id, _today, false, null, true, Guid.NewGuid()), CancellationToken.None);

        result.Error.Should().Be(GymErrors.AppLoginRequired);
    }

    [Fact]
    public void CreateMember_RequiresEmailWhenAppAccessEnabled()
    {
        var validator = new CreateMemberCommandValidator();
        var invalid = validator.Validate(new CreateMemberCommand(
            "", null, "Rahul", "Sharma", "9876543210", null, null, null, false,
            JoinDate: _today, AssignAppAccess: true, AppSubscriptionId: Guid.NewGuid()));
        invalid.IsValid.Should().BeFalse();

        var valid = validator.Validate(new CreateMemberCommand(
            "rahul@gym.test", null, "Rahul", "Sharma", "9876543210", null, null, null, false,
            JoinDate: _today, AssignAppAccess: false));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SeatAllocator_RejectsWhenPoolIsFull()
    {
        SeatAllocator.HasCapacity(50, 50).Should().BeFalse();
        SeatAllocator.HasCapacity(50, 49).Should().BeTrue();
    }

    private CreateMembershipCommandHandler MembershipHandler(IPeopleRepository people, IMembershipRepository memberships, IMemberAppSeatAssigner seats)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(true);
        var ledger = Substitute.For<LifeFitAI.Application.Gym.Ledger.ILedgerPostingService>();
        ledger.TryPostMembershipPaymentAsync(Arg.Any<Guid>(), Arg.Any<LifeFitAI.Domain.Gym.MembershipPayment>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return new CreateMembershipCommandHandler(
            Guard(),
            Actor(),
            permissions,
            people,
            Substitute.For<IBranchRepository>(),
            memberships,
            Substitute.For<LifeFitAI.Application.Gym.Payments.IMembershipPaymentRepository>(),
            ledger,
            Substitute.For<LifeFitAI.Application.Gym.Configuration.IBusinessConfigRepository>(),
            seats,
            Substitute.For<IAuditLogRepository>(),
            Unit(),
            Clock(),
            Substitute.For<IClientContext>());
    }

    private AssignMemberAppPlanCommandHandler AssignHandler(IPeopleRepository people, IMemberAppSeatAssigner seats, bool licenseManage)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(licenseManage);
        return new AssignMemberAppPlanCommandHandler(Actor(), permissions, Guard(), people, seats, Unit());
    }

    private IOperationGuard Guard()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_actorId);
        actor.TenantId.Returns(_tenantA);
        return actor;
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
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        return unit;
    }
}
