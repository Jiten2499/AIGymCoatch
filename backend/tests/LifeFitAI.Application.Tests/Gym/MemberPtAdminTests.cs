using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MemberPtAdminTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _other = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _memberId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly Guid _trainerId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1");

    [Fact]
    public async Task Assign_IsRejected_WhenTheCallerLacksMemberUpdate()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        var handler = AssignHandler(packages, Result.Failure(IdentityErrors.Forbidden));

        var result = await handler.Handle(new AssignMemberPtPackageCommand(_memberId, Guid.NewGuid(), null, new DateOnly(2026, 10, 24)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.Forbidden);
        await packages.DidNotReceive().AddAssignmentAsync(Arg.Any<MemberPtAssignment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_OpensThePackage_OnlyInsideTheActorTenant()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Starter", null, 8, 30, 100, "USD");
        packages.GetAsync(_tenant, package.Id, Arg.Any<CancellationToken>()).Returns(package);
        packages.HasActiveAssignmentAsync(_tenant, _memberId, package.Id, Arg.Any<CancellationToken>()).Returns(false);
        var people = People();
        var unit = Substitute.For<IUnitOfWork>();
        var handler = new AssignMemberPtPackageCommandHandler(Guard(Result.Success()), Actor(_tenant), people, packages, unit);

        var result = await handler.Handle(new AssignMemberPtPackageCommand(_memberId, package.Id, null, new DateOnly(2026, 10, 24)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await people.Received(1).GetMemberAsync(_tenant, _memberId, Arg.Any<CancellationToken>());
        await people.DidNotReceive().GetMemberAsync(_other, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await packages.Received(1).AddAssignmentAsync(
            Arg.Is<MemberPtAssignment>(item => item.TenantId == _tenant && item.MemberProfileId == _memberId && item.SessionsRemaining == 8),
            Arg.Any<CancellationToken>());
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_DoesNotOpen_WhenTheMemberIsOutsideTheTenant()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenant, _memberId, Arg.Any<CancellationToken>()).Returns((MemberProfile?)null);
        var handler = new AssignMemberPtPackageCommandHandler(Guard(Result.Success()), Actor(_tenant), people, packages, Substitute.For<IUnitOfWork>());

        var result = await handler.Handle(new AssignMemberPtPackageCommand(_memberId, Guid.NewGuid(), _trainerId, new DateOnly(2026, 10, 24)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GymErrors.NotFound);
        await packages.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordedSession_DeductsOneEntitlement_AndDoesNotDeductAgain()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Starter", null, 8, 30, 100, "USD");
        var assignment = MemberPtAssignment.Open(Guid.NewGuid(), package, _memberId, _trainerId, new DateOnly(2026, 10, 1));
        var people = People();
        people.GetTrainerAsync(_tenant, _trainerId, Arg.Any<CancellationToken>()).Returns(TrainerProfile.Create(_trainerId, _tenant, Guid.NewGuid(), null));
        packages.GetAssignmentAsync(_tenant, assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        packages.SessionAlreadyConsumedAsync(_tenant, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var handler = SessionHandler(packages, people);
        var command = new CompletePtSessionCommand(_memberId, _trainerId, new DateOnly(2026, 10, 24), null, assignment.Id, Guid.NewGuid());

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        assignment.SessionsRemaining.Should().Be(7);
        await packages.Received(1).AddConsumptionAsync(Arg.Any<PtSessionConsumption>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordedSession_IsRejected_WithoutTrainerUpdate()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        var handler = SessionHandler(packages, People(), Result.Failure(IdentityErrors.Forbidden));

        var result = await handler.Handle(new CompletePtSessionCommand(_memberId, _trainerId, new DateOnly(2026, 10, 24), null, Guid.NewGuid(), null), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.Forbidden);
        await packages.DidNotReceive().AddConsumptionAsync(Arg.Any<PtSessionConsumption>(), Arg.Any<CancellationToken>());
    }

    private IPeopleRepository People()
    {
        var people = Substitute.For<IPeopleRepository>();
        people.GetMemberAsync(_tenant, _memberId, Arg.Any<CancellationToken>()).Returns(MemberProfile.Create(_memberId, _tenant, null, "M-1", new DateOnly(2026, 10, 1), "Ada", "Lovelace"));
        return people;
    }

    private AssignMemberPtPackageCommandHandler AssignHandler(IPtPackageRepository packages, Result gate) =>
        new(Guard(gate), Actor(_tenant), People(), packages, Substitute.For<IUnitOfWork>());

    private CompletePtSessionCommandHandler SessionHandler(IPtPackageRepository packages, IPeopleRepository people, Result? gate = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc));
        return new CompletePtSessionCommandHandler(Guard(gate ?? Result.Success()), Actor(_tenant), Substitute.For<IMembershipRepository>(), people, packages, Substitute.For<IUnitOfWork>(), clock);
    }

    private static IOperationGuard Guard(Result gate)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate);
        return guard;
    }

    private static ICurrentActor Actor(Guid tenantId)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(tenantId);
        actor.UserId.Returns(Guid.NewGuid());
        return actor;
    }
}
