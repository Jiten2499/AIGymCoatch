using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Payroll;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class PayrollWorkflowTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Approve_RequiresPermissionAndConfirmation()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.PayrollApprove, FeatureCodes.GymStaffPayroll, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(GymErrors.FeatureDisabled));
        var handler = new PayrollApprovePayHandler(guard, Actor(_tenantA), Substitute.For<IPayrollStore>(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var denied = await handler.Handle(new ApprovePayrollCommand(Guid.NewGuid(), true), CancellationToken.None);
        denied.Error.Should().Be(GymErrors.FeatureDisabled);

        guard.AllowAsync(PermissionCodes.PayrollApprove, FeatureCodes.GymStaffPayroll, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var store = Substitute.For<IPayrollStore>();
        var run = PayrollRun.Create(Guid.NewGuid(), _tenantA, 2026, 10, "INR", _userId, _utc);
        run.Submit(_utc);
        store.GetRunAsync(_tenantA, run.Id, Arg.Any<CancellationToken>()).Returns(run);
        store.ListLinesAsync(_tenantA, run.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<PayrollLine>());
        var allowed = new PayrollApprovePayHandler(guard, Actor(_tenantA), store, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        (await allowed.Handle(new ApprovePayrollCommand(run.Id, false), CancellationToken.None)).Error.Should().Be(GymErrors.InvalidState);
        (await allowed.Handle(new ApprovePayrollCommand(run.Id, true), CancellationToken.None)).Error.Should().Be(GymErrors.InvalidState);
    }

    [Fact]
    public async Task Approve_LocksSnapshotAndBlocksRewrite()
    {
        var run = PayrollRun.Create(Guid.NewGuid(), _tenantA, 2026, 10, "INR", _userId, _utc);
        run.Submit(_utc);
        var line = PayrollLine.Create(Guid.NewGuid(), _tenantA, run.Id, Guid.NewGuid(), _userId, "Sam Admin", "S1", "Manager", 30000m, 2000m, 1000m, false, false, false);
        line.SetAdjustments(500m, 250m);
        var store = StoreWithRun(run, line);
        var guard = Allow(PermissionCodes.PayrollApprove);
        var handler = new PayrollApprovePayHandler(guard, Actor(_tenantA), store, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new ApprovePayrollCommand(run.Id, true), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(PayrollRunStatus.Approved);
        line.Locked.Should().BeTrue();
        line.GrossPay.Should().Be(33500m);
        line.NetPay.Should().Be(33250m);
        Assert.Throws<InvalidOperationException>(() => line.SetAdjustments(1m, 0m));
    }

    [Fact]
    public async Task Payment_RejectsDuplicateReference_AndSupportsPartial()
    {
        var run = PayrollRun.Create(Guid.NewGuid(), _tenantA, 2026, 10, "INR", _userId, _utc);
        run.Approve(_userId, _utc);
        var line = PayrollLine.Create(Guid.NewGuid(), _tenantA, run.Id, Guid.NewGuid(), _userId, "Sam Admin", "S1", "Manager", 1000m, 0m, 0m, false, false, false);
        line.Lock();
        var store = StoreWithRun(run, line);
        store.PaymentReferenceExistsAsync(_tenantA, line.Id, "REF-1", Arg.Any<CancellationToken>()).Returns(true);
        store.ListAdjustmentsAsync(_tenantA, line.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<PayrollAdjustment>());
        store.ListPaymentsAsync(_tenantA, line.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<PayrollPayment>());
        var handler = new PayrollApprovePayHandler(Allow(PermissionCodes.PayrollPay), Actor(_tenantA), store, Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var duplicate = await handler.Handle(new RecordPayrollPaymentCommand(line.Id, 100m, new DateOnly(2026, 10, 20), PayrollPaymentMethod.BankTransfer, "REF-1"), CancellationToken.None);
        duplicate.Error.Should().Be(GymErrors.Conflict);

        store.PaymentReferenceExistsAsync(_tenantA, line.Id, "REF-2", Arg.Any<CancellationToken>()).Returns(false);
        var partial = await handler.Handle(new RecordPayrollPaymentCommand(line.Id, 400m, new DateOnly(2026, 10, 20), PayrollPaymentMethod.BankTransfer, "REF-2"), CancellationToken.None);
        partial.IsSuccess.Should().BeTrue();
        line.PaymentStatus.Should().Be(PayrollPaymentStatus.PartiallyPaid);
        run.Status.Should().Be(PayrollRunStatus.PartiallyPaid);
    }

    [Fact]
    public async Task GetRun_IsTenantIsolated()
    {
        var run = PayrollRun.Create(Guid.NewGuid(), _tenantA, 2026, 10, "INR", _userId, _utc);
        var store = Substitute.For<IPayrollStore>();
        store.GetRunAsync(_tenantB, run.Id, Arg.Any<CancellationToken>()).Returns((PayrollRun?)null);
        var guard = Allow(PermissionCodes.PayrollRead);
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(_userId, _tenantB, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var queries = new PayrollQueryHandler(guard, Actor(_tenantB), store, permissions);
        var result = await queries.Handle(new GetPayrollRunQuery(run.Id), CancellationToken.None);
        result.Error.Should().Be(GymErrors.NotFound);
    }

    private IPayrollStore StoreWithRun(PayrollRun run, PayrollLine line)
    {
        var store = Substitute.For<IPayrollStore>();
        store.GetRunAsync(_tenantA, run.Id, Arg.Any<CancellationToken>()).Returns(run);
        store.GetLineAsync(_tenantA, line.Id, Arg.Any<CancellationToken>()).Returns(line);
        store.ListLinesAsync(_tenantA, run.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        store.ListAdjustmentsAsync(_tenantA, line.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<PayrollAdjustment>());
        store.ListPaymentsAsync(_tenantA, line.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<PayrollPayment>());
        return store;
    }

    private static IOperationGuard Allow(string permission)
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(permission, FeatureCodes.GymStaffPayroll, Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor(Guid tenantId)
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.TenantId.Returns(tenantId);
        actor.UserId.Returns(_userId);
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
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return unit;
    }
}
