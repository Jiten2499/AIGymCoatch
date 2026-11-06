using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class SystemJobAdminTests
{
    private readonly Guid _tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");

    [Fact]
    public async Task GymUser_CannotManageSystemJobs()
    {
        var handler = Handler(superAdmin: false, manage: true);
        var list = await handler.Handle(new ListSystemJobsQuery(), CancellationToken.None);
        list.Error.Should().Be(IdentityErrors.Forbidden);
        var run = await handler.Handle(new RunSystemJobCommand(SystemJobCatalog.MembershipExpiryKey), CancellationToken.None);
        run.Error.Should().Be(IdentityErrors.Forbidden);
    }

    [Fact]
    public async Task RunNow_RequiresTheJobToBeEnabledAndAudits()
    {
        var job = Definition(true);
        var catalog = Substitute.For<ISystemJobCatalog>();
        catalog.FindAsync(SystemJobCatalog.MembershipExpiryKey, Arg.Any<CancellationToken>()).Returns(job);
        var scheduler = Substitute.For<IBackgroundJobScheduler>();
        scheduler.EnqueueSystemJob(Arg.Any<RecurringJobRegistration>(), Arg.Any<BackgroundJobMetadata>()).Returns("job-1");
        var audit = Substitute.For<IAuditLogRepository>();
        var handler = Handler(superAdmin: true, manage: true, catalog, scheduler, audit);
        var denied = await Handler(superAdmin: true, manage: false, catalog, scheduler, audit).Handle(new RunSystemJobCommand(job.JobKey), CancellationToken.None);
        denied.Error.Should().Be(IdentityErrors.Forbidden);
        (await handler.Handle(new RunSystemJobCommand(job.JobKey), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await audit.Received(1).AddAsync(Arg.Is<AuditLog>(entry => entry.Action == "SystemJobRunNow"), Arg.Any<CancellationToken>());
        scheduler.Received(1).EnqueueSystemJob(Arg.Is<RecurringJobRegistration>(item => item.JobKey == job.JobKey), Arg.Is<BackgroundJobMetadata>(item => item.JobType == job.JobType && item.Queue == "notifications"));

        var disabled = Definition(false);
        catalog.FindAsync(SystemJobCatalog.MembershipExpiryKey, Arg.Any<CancellationToken>()).Returns(disabled);
        var blocked = await handler.Handle(new RunSystemJobCommand(job.JobKey), CancellationToken.None);
        blocked.Error.Should().Be(SystemJobErrors.Disabled);
    }

    [Fact]
    public async Task Disable_StopsTheRecurringRegistration()
    {
        var job = Definition(true);
        var catalog = Substitute.For<ISystemJobCatalog>();
        catalog.FindAsync(job.JobKey, Arg.Any<CancellationToken>()).Returns(job);
        var scheduler = Substitute.For<IBackgroundJobScheduler>();
        var audit = Substitute.For<IAuditLogRepository>();
        var handler = Handler(superAdmin: true, manage: true, catalog, scheduler, audit);
        (await handler.Handle(new DisableSystemJobCommand(job.JobKey), CancellationToken.None)).IsSuccess.Should().BeTrue();
        job.IsEnabled.Should().BeFalse();
        scheduler.Received(1).SyncRecurring(Arg.Is<RecurringJobRegistration>(item => item.JobKey == job.JobKey && !item.IsEnabled));
        await audit.Received(1).AddAsync(Arg.Is<AuditLog>(entry => entry.Action == "SystemJobDisabled"), Arg.Any<CancellationToken>());
    }

    private SystemJobHandlers Handler(bool superAdmin, bool manage, ISystemJobCatalog? catalog = null, IBackgroundJobScheduler? scheduler = null, IAuditLogRepository? audit = null)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenant);
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(_userId, _tenant, Arg.Any<CancellationToken>()).Returns(superAdmin ? [RoleNames.SuperAdmin] : ["GymOwner"]);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenant, PermissionCodes.BackgroundJobRead, Arg.Any<CancellationToken>()).Returns(true);
        permissions.HasPermissionAsync(_userId, _tenant, PermissionCodes.BackgroundJobManage, Arg.Any<CancellationToken>()).Returns(manage);
        permissions.HasPermissionAsync(_userId, _tenant, PermissionCodes.PlatformAdmin, Arg.Any<CancellationToken>()).Returns(superAdmin);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc));
        return new SystemJobHandlers(actor, roles, permissions, catalog ?? Substitute.For<ISystemJobCatalog>(), Substitute.For<ISystemJobRuntime>(), scheduler ?? Substitute.For<IBackgroundJobScheduler>(), audit ?? Substitute.For<IAuditLogRepository>(), Substitute.For<IUnitOfWork>(), clock, Substitute.For<IClientContext>());
    }

    private static SystemJobDefinition Definition(bool enabled) =>
        SystemJobDefinition.Create(Guid.NewGuid(), SystemJobCatalog.MembershipExpiryKey, "Membership Expiry Notification", "Remind members", SystemJobCatalog.MembershipExpiryType, SystemJobCatalog.DailyCron, SystemJobCatalog.NotificationsQueue, enabled, FeatureCodes.GymCommunications, DateTime.UtcNow);
}
