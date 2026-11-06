using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class PtPackageConfigurationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Create_StoresAnActiveTenantPackageAndRejectsInvalidData()
    {
        var packages = Substitute.For<IPtPackageRepository>();
        PtPackage? saved = null;
        packages.AddAsync(Arg.Do<PtPackage>(item => saved = item), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = CreateHandler(packages, BusinessProfile.Create(Guid.NewGuid(), _tenantId, "Asia/Kolkata", "INR"));

        var created = await handler.Handle(new CreatePtPackageCommand("Personal Training — 12 Sessions", "Coached sessions", 12, 60, 6000m, "inr"), CancellationToken.None);
        var invalid = await new CreatePtPackageCommandValidator().ValidateAsync(new CreatePtPackageCommand("", null, 0, 0, -1m, "US", "Inactive"));

        created.IsSuccess.Should().BeTrue();
        created.Value.Currency.Should().Be("INR");
        created.Value.Status.Should().Be("Active");
        created.Value.SessionCount.Should().Be(12);
        saved!.TenantId.Should().Be(_tenantId);
        invalid.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAndStatus_KeepThePackageAndRejectAnotherTenantsCurrency()
    {
        var package = PtPackage.Create(Guid.NewGuid(), _tenantId, "Starter", null, 8, 30, 100m, "INR");
        var assignment = MemberPtAssignment.Open(Guid.NewGuid(), package, Guid.NewGuid(), null, new DateOnly(2026, 10, 1));
        var packages = Substitute.For<IPtPackageRepository>();
        packages.GetAsync(_tenantId, package.Id, Arg.Any<CancellationToken>()).Returns(package);
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), _tenantId, "UTC", "INR"));
        var updater = new UpdatePtPackageCommandHandler(Allow(), Actor(), packages, profiles, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var status = new SetPtPackageStatusCommandHandler(Allow(), Actor(), packages, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());

        var edited = await updater.Handle(new UpdatePtPackageCommand(package.Id, "Starter plus", "Updated", 10, 45, 150m, "INR"), CancellationToken.None);
        var wrongCurrency = await updater.Handle(new UpdatePtPackageCommand(package.Id, "Starter plus", null, 10, 45, 150m, "USD"), CancellationToken.None);
        var inactive = await status.Handle(new SetPtPackageStatusCommand(package.Id, "Inactive"), CancellationToken.None);

        edited.IsSuccess.Should().BeTrue();
        edited.Value.SessionCount.Should().Be(10);
        assignment.PackageName.Should().Be("Starter");
        assignment.SessionCount.Should().Be(8);
        wrongCurrency.Error.Code.Should().Be("gym.invalid_state");
        inactive.Value.Status.Should().Be("Inactive");
        inactive.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task List_ReturnsOnlyActivePackagesForTheCurrentTenant()
    {
        var active = PtPackage.Create(Guid.NewGuid(), _tenantId, "Current", null, 4, 30, 40m, "INR");
        var draft = PtPackage.Create(Guid.NewGuid(), _tenantId, "Draft pack", null, 4, 30, 40m, "INR");
        draft.MarkDraft();
        var packages = Substitute.For<IPtPackageRepository>();
        packages.ListAsync(_tenantId, true, Arg.Any<CancellationToken>()).Returns(new[] { active });
        packages.GetAsync(_tenantId, Guid.Empty, Arg.Any<CancellationToken>()).Returns((PtPackage?)null);
        var listed = await new ListPtPackagesQueryHandler(Allow(), Actor(), packages).Handle(new ListPtPackagesQuery(true), CancellationToken.None);
        var missing = await new GetPtPackageQueryHandler(Allow(), Actor(), packages).Handle(new GetPtPackageQuery(Guid.Empty), CancellationToken.None);

        listed.Value.Should().ContainSingle(item => item.Name == "Current" && item.IsActive);
        listed.Value.Should().NotContain(item => item.Status == "Draft");
        missing.Error.Code.Should().Be("gym.not_found");
        await packages.Received(1).ListAsync(_tenantId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RenewalDoesNotTurnIncludedSessionsIntoAStandalonePackage()
    {
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantId, "Monthly", null, 30, MembershipType.Monthly, null, 100m, "INR", includedPtSessions: 6);
        var current = GymMembership.Create(Guid.NewGuid(), _tenantId, Guid.NewGuid(), null, plan.Id, "G1", new DateOnly(2026, 10, 1), 31, new DateOnly(2026, 10, 9), false, null);
        current.ApplyIncludedSessions(4);
        current.TryConsumeIncludedSession(new DateOnly(2026, 10, 9));

        var successor = MembershipRenewalRules.CreateSuccessor(current, plan, new DateOnly(2026, 10, 9), new DateOnly(2026, 11, 1), true, Guid.NewGuid(), "G2", false, null);

        successor!.IncludedPtSessions.Should().Be(6);
        successor.PtSessionsRemaining.Should().Be(6);
        current.PtSessionsRemaining.Should().Be(3);
    }

    private CreatePtPackageCommandHandler CreateHandler(IPtPackageRepository packages, BusinessProfile profile)
    {
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(profile);
        return new CreatePtPackageCommandHandler(Allow(), Actor(), packages, profiles, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
    }

    private IOperationGuard Allow()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(_tenantId);
        actor.UserId.Returns(_userId);
        return actor;
    }

    private static IUnitOfWork Unit() => Substitute.For<IUnitOfWork>();

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
        return clock;
    }
}
