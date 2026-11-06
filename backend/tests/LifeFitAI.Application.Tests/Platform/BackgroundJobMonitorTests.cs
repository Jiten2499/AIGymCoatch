using FluentAssertions;
using Hangfire;
using LifeFitAI.Application.Authorization;
using Hangfire.InMemory;
using Hangfire.States;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class BackgroundJobMonitorTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");

    [Fact]
    public async Task List_FiltersPagesAndHidesArguments()
    {
        var (monitor, scheduler) = Create();
        var first = scheduler.Enqueue(Meta("Maintenance.Cleanup", _tenantA, "corr-1", "Maintenance", "cleanup"), () => BackgroundJobProbe.RunAsync("apiKey=super-secret"));
        scheduler.Enqueue(Meta("Communication.Publish", _tenantA, "corr-2", "Communication", "comm-1"), () => BackgroundJobProbe.RunAsync("token=abc"));
        scheduler.Enqueue(Meta("Maintenance.Cleanup", _tenantB, "corr-3", "Maintenance", "other"), () => BackgroundJobProbe.RunAsync("password=nope"));

        var page = await monitor.SearchAsync(new BackgroundJobSearch("Cleanup", null, BackgroundJobStatus.Queued, "default", _tenantA, null, null, null, "Maintenance", 1, 1, "createdAt", true), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.Items[0].JobId.Should().Be(first);
        page.Items[0].SourceId.Should().Be("cleanup");
        page.Items[0].ErrorMessage.Should().BeNull();
        var details = await monitor.GetAsync(first, CancellationToken.None);
        details!.History.Should().NotBeEmpty();
        details.ToString().Should().NotContain("super-secret");
    }

    [Fact]
    public async Task FailedJob_CanRetry_AndCompletedJobCannotBeCancelled()
    {
        var (monitor, scheduler) = Create();
        var failed = scheduler.Enqueue(Meta("Report.Generate", _tenantA, "corr", "Report", "r1"), () => BackgroundJobProbe.RunAsync("password=hunter2"));
        using (var connection = scheduler.Storage.GetConnection())
        using (var transaction = connection.CreateWriteTransaction())
        {
            transaction.SetJobState(failed, new FailedState(new InvalidOperationException("password=hunter2 bearer abc.def")));
            transaction.Commit();
        }

        var details = await monitor.GetAsync(failed, CancellationToken.None);
        details!.Job.Status.Should().Be(BackgroundJobStatus.Failed);
        details.Job.ErrorMessage.Should().NotContain("hunter2");
        details.Job.ErrorMessage.Should().Contain("(redacted)");
        (await monitor.RetryAsync(failed, CancellationToken.None)).Should().BeTrue();
        (await monitor.GetAsync(failed, CancellationToken.None))!.Job.Status.Should().Be(BackgroundJobStatus.Queued);

        var done = scheduler.Enqueue(Meta("Report.Generate", _tenantA, "corr", "Report", "r2"), () => BackgroundJobProbe.RunAsync("ok"));
        using (var connection = scheduler.Storage.GetConnection())
        using (var transaction = connection.CreateWriteTransaction())
        {
            transaction.SetJobState(done, new SucceededState(null, 0, 0));
            transaction.Commit();
        }

        var handler = Handler(monitor, superAdmin: true, manage: true);
        var cancel = await handler.Handle(new CancelBackgroundJobCommand(done), CancellationToken.None);
        cancel.Error.Should().Be(BackgroundJobErrors.InvalidState);
    }

    [Fact]
    public async Task QueuedJob_CanBeCancelled_AndFailedJobCannot()
    {
        var (monitor, scheduler) = Create();
        var queued = scheduler.Enqueue(Meta("Maintenance.Cleanup", _tenantA, "c", "Maintenance", "1"), () => BackgroundJobProbe.RunAsync("x"));
        var handler = Handler(monitor, superAdmin: true, manage: true);
        var cancelled = await handler.Handle(new CancelBackgroundJobCommand(queued), CancellationToken.None);
        cancelled.IsSuccess.Should().BeTrue();
        (await monitor.GetAsync(queued, CancellationToken.None))!.Job.Status.Should().Be(BackgroundJobStatus.Deleted);

        var failed = scheduler.Enqueue(Meta("Maintenance.Cleanup", _tenantA, "c", "Maintenance", "2"), () => BackgroundJobProbe.RunAsync("x"));
        using (var connection = scheduler.Storage.GetConnection())
        using (var transaction = connection.CreateWriteTransaction())
        {
            transaction.SetJobState(failed, new FailedState(new InvalidOperationException("timeout")));
            transaction.Commit();
        }

        var denied = await handler.Handle(new CancelBackgroundJobCommand(failed), CancellationToken.None);
        denied.Error.Should().Be(BackgroundJobErrors.InvalidState);
    }

    [Fact]
    public async Task Summary_CountsQueuedJobs()
    {
        var (monitor, scheduler) = Create();
        scheduler.Enqueue(Meta("Ai.Process", _tenantA, "c", "Ai", "1"), () => BackgroundJobProbe.RunAsync("x"));
        var summary = await monitor.SummaryAsync(_tenantA, CancellationToken.None);
        summary.Queued.Should().Be(1);
        summary.Failed.Should().Be(0);
    }

    [Fact]
    public async Task BusinessAdmin_CannotSeeAnotherTenant()
    {
        var (monitor, scheduler) = Create();
        var foreign = scheduler.Enqueue(Meta("Crm.Reminder", _tenantB, "c", "Lead", "9"), () => BackgroundJobProbe.RunAsync("x"));
        var handler = Handler(monitor, superAdmin: false, manage: false);
        var list = await handler.Handle(new ListBackgroundJobsQuery(null, null, null, null, _tenantB, null, null, null, null, 1, 20, null, true), CancellationToken.None);
        list.IsSuccess.Should().BeTrue();
        list.Value.Items.Should().BeEmpty();
        var read = await handler.Handle(new GetBackgroundJobQuery(foreign), CancellationToken.None);
        read.Error.Should().Be(BackgroundJobErrors.NotFound);
    }

    [Fact]
    public async Task SuperAdmin_CanSeeAcrossTenants_AndRetryRequiresManage()
    {
        var (monitor, scheduler) = Create();
        var job = scheduler.Enqueue(Meta("File.Process", _tenantB, "c", "File", "f"), () => BackgroundJobProbe.RunAsync("x"));
        var reader = Handler(monitor, superAdmin: true, manage: false);
        var list = await reader.Handle(new ListBackgroundJobsQuery(null, null, null, null, _tenantB, null, null, null, null, 1, 20, null, true), CancellationToken.None);
        list.Value.Items.Should().ContainSingle().Which.JobId.Should().Be(job);
        var denied = await reader.Handle(new RetryBackgroundJobCommand(job), CancellationToken.None);
        denied.Error.Should().Be(IdentityErrors.Forbidden);
    }

    private static (HangfireBackgroundJobMonitor Monitor, HangfireJobScheduler Scheduler) Create()
    {
        var storage = new InMemoryStorage();
        var tenants = Substitute.For<ITenantRepository>();
        return (new HangfireBackgroundJobMonitor(storage, null, tenants), new HangfireJobScheduler(new BackgroundJobClient(storage), storage));
    }

    private BackgroundJobHandlers Handler(IBackgroundJobMonitor monitor, bool superAdmin, bool manage)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_userId);
        actor.TenantId.Returns(_tenantA);
        var roles = Substitute.For<IRoleRepository>();
        roles.GetRoleNamesAsync(_userId, _tenantA, Arg.Any<CancellationToken>()).Returns(superAdmin ? [RoleNames.SuperAdmin] : ["GymOwner"]);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.BackgroundJobRead, Arg.Any<CancellationToken>()).Returns(true);
        permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.BackgroundJobManage, Arg.Any<CancellationToken>()).Returns(manage);
        permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.PlatformAdmin, Arg.Any<CancellationToken>()).Returns(superAdmin);
        var audit = Substitute.For<IAuditLogRepository>();
        var unit = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return new BackgroundJobHandlers(actor, roles, permissions, monitor, audit, unit, clock, Substitute.For<IClientContext>());
    }

    private static BackgroundJobMetadata Meta(string type, Guid tenantId, string correlationId, string sourceType, string sourceId) =>
        new(type, "default", tenantId, correlationId, sourceType, sourceId, null);
}

public static class BackgroundJobProbe
{
    public static Task RunAsync(string secret) => Task.CompletedTask;
}
