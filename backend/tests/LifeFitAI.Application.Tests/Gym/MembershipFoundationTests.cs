using System.Security.Claims;
using FluentAssertions;
using LifeFitAI.Api.Middleware;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Gym.Memberships;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class MembershipFoundationTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");

    [Theory]
    [InlineData(2026, 1, 15, 1, 2026, 2, 15)]
    [InlineData(2025, 1, 31, 1, 2025, 2, 28)]
    [InlineData(2024, 1, 31, 1, 2024, 2, 29)]
    [InlineData(2026, 1, 15, 3, 2026, 4, 15)]
    public void CalendarMonths_UseAnniversaryAndClampMonthEnd(int year, int month, int day, int count, int endYear, int endMonth, int endDay)
    {
        MembershipCalendar.InclusiveEnd(new DateOnly(year, month, day), count, PackageDurationUnit.Month)
            .Should().Be(new DateOnly(endYear, endMonth, endDay));
    }

    [Fact]
    public async Task Term_UsesThePackageCalendarAndRejectsInactivePackages()
    {
        var start = new DateOnly(2026, 1, 15);
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Monthly", null, 30, MembershipType.Monthly, null, 1500, "INR", PackageCategory.Standard, PackageDurationUnit.Month, 1);
        var plans = Substitute.For<IMembershipRepository>();
        plans.GetPlanAsync(_tenantA, plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new GetMembershipTermQueryHandler(Guard(null), Actor(), plans, Clock());

        var term = await handler.Handle(new GetMembershipTermQuery(plan.Id, start), CancellationToken.None);

        term.IsSuccess.Should().BeTrue();
        term.Value.EndDate.Should().Be(plan.InclusiveEnd(start));
        term.Value.Status.Should().Be(GymMembershipStatus.Expired);
        plan.Deactivate();
        (await handler.Handle(new GetMembershipTermQuery(plan.Id, start), CancellationToken.None)).Error.Should().Be(GymErrors.InvalidState);
    }

    [Fact]
    public void Assignment_AllowsAMembershipWithoutABranch()
    {
        var membership = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), null, Guid.NewGuid(), "G00000001", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Today, false, null);
        membership.BranchId.Should().BeNull();
        membership.ScheduleRenewal(Today, 1, PackageDurationUnit.Month, Guid.NewGuid(), "G00000002", false, null)!.BranchId.Should().BeNull();
    }

    [Fact]
    public void Renewal_PreservesHistoryBeforeAndAfterExpiry()
    {
        var current = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "G11111111", new DateOnly(2026, 1, 1), 31, Today, false, null);
        var early = current.ScheduleRenewal(new DateOnly(2026, 1, 15), 1, PackageDurationUnit.Month, Guid.NewGuid(), "G22222222", false, null);
        current.StartDate.Should().Be(new DateOnly(2026, 1, 1));
        current.EndDate.Should().Be(new DateOnly(2026, 1, 31));
        early!.StartDate.Should().Be(new DateOnly(2026, 2, 1));
        early.EndDate.Should().Be(new DateOnly(2026, 3, 1));
        early.Status.Should().Be(GymMembershipStatus.Pending);

        var expired = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "G33333333", new DateOnly(2026, 1, 1), 10, Today, false, null);
        expired.Expire(Today);
        var restarted = expired.ScheduleRenewal(Today, 1, PackageDurationUnit.Month, Guid.NewGuid(), "G44444444", false, null);
        expired.Status.Should().Be(GymMembershipStatus.Expired);
        expired.EndDate.Should().Be(new DateOnly(2026, 1, 10));
        restarted!.StartDate.Should().Be(Today);
        restarted.Status.Should().Be(GymMembershipStatus.Active);
    }

    [Fact]
    public void FreezeExtensionAndCancellation_KeepTheMembership()
    {
        var membership = GymMembership.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "G55555555", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Today, false, null);
        var id = membership.Id;
        membership.Freeze(Today).Should().BeTrue();
        membership.Freeze(Today).Should().BeFalse();
        var freeze = MembershipFreeze.Open(Guid.NewGuid(), _tenantA, id, Today, GymMembershipStatus.Active, "travel", DateTime.UtcNow);
        freeze.Close(Today.AddDays(-1)).Should().BeFalse();
        membership.ExtendForFreeze(7).Should().BeTrue();
        membership.EndDate.Should().Be(new DateOnly(2026, 11, 7));
        freeze.Close(Today.AddDays(6)).Should().BeTrue();
        membership.Unfreeze(Today).Should().BeTrue();
        membership.Extend(7).Should().BeTrue();
        membership.EndDate.Should().Be(new DateOnly(2026, 11, 14));
        membership.Cancel().Should().BeTrue();
        membership.Extend(1).Should().BeFalse();
        membership.Id.Should().Be(id);
        membership.Status.Should().Be(GymMembershipStatus.Cancelled);
        MembershipCalendar.EffectiveStatus(GymMembershipStatus.Active, membership.StartDate, new DateOnly(2026, 10, 1), Today).Should().Be(GymMembershipStatus.Expired);
    }

    [Fact]
    public async Task Search_IsTenantScoped_AndPaginates()
    {
        await using var db = CreateDb();
        var planA = MembershipPlan.Create(Guid.NewGuid(), _tenantA, "Annual Pro", null, 12, MembershipType.Yearly, null, 100m, "USD", PackageCategory.Standard, PackageDurationUnit.Month, 12);
        var planB = MembershipPlan.Create(Guid.NewGuid(), _tenantB, "Annual Pro", null, 12, MembershipType.Yearly, null, 100m, "USD");
        db.MembershipPlans.AddRange(planA, planB);
        Stamp(db, planA, planB);
        var ada = User("ada@gym.test", "+15551110001", "Ada", "Lovelace", _tenantA);
        var other = User("other@gym.test", "+15551110002", "Other", "Gym", _tenantB);
        db.Users.AddRange(ada, other);
        Stamp(db, ada, other);
        var memberA = MemberProfile.Create(Guid.NewGuid(), _tenantA, ada.Id, "M-ADA", Today);
        var memberB = MemberProfile.Create(Guid.NewGuid(), _tenantB, other.Id, "M-OTHER", Today);
        db.MemberProfiles.AddRange(memberA, memberB);
        Stamp(db, memberA, memberB);
        var first = GymMembership.Create(Guid.NewGuid(), _tenantA, memberA.Id, Guid.NewGuid(), planA.Id, "G-ADA-1", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Today, false, null);
        first.Expire(Today);
        var second = GymMembership.Create(Guid.NewGuid(), _tenantA, memberA.Id, Guid.NewGuid(), planA.Id, "G-ADA-2", Today, planA.InclusiveEnd(Today), Today, false, null);
        var foreign = GymMembership.Create(Guid.NewGuid(), _tenantB, memberB.Id, Guid.NewGuid(), planB.Id, "G-OTHER", Today, new DateOnly(2026, 12, 6), Today, false, null);
        db.GymMemberships.AddRange(first, second, foreign);
        Stamp(db, first, second, foreign);
        await db.SaveChangesAsync();
        var repository = new MembershipRepository(db);

        var plans = await repository.ListPlansAsync(_tenantA, null, "Annual", null, new PageQuery(1, 1), CancellationToken.None);
        plans.TotalCount.Should().Be(1);
        plans.Items.Should().ContainSingle(plan => plan.Id == planA.Id);

        var page = await repository.SearchMembershipsAsync(_tenantA, null, null, null, memberA.Id, "Ada", null, null, null, null, Today, "startDate", new PageQuery(1, 1), CancellationToken.None);
        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.Items[0].MemberName.Should().Be("Ada Lovelace");
        page.Items.Should().NotContain(item => item.Id == foreign.Id);
        (await repository.GetPlanAsync(_tenantA, planB.Id, CancellationToken.None)).Should().BeNull();
        (await repository.GetMembershipAsync(_tenantA, foreign.Id, CancellationToken.None)).Should().BeNull();
        (await repository.OverlapsMembershipAsync(_tenantA, memberA.Id, Today, second.EndDate, null, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Assignment_RejectsInactivePlansAndOtherTenants()
    {
        var memberId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var people = Substitute.For<IPeopleRepository>();
        var branches = Substitute.For<IBranchRepository>();
        var memberships = Substitute.For<IMembershipRepository>();
        people.GetMemberAsync(_tenantA, memberId, Arg.Any<CancellationToken>()).Returns(MemberProfile.Create(memberId, _tenantA, Guid.NewGuid(), "M1", Today));
        branches.GetAsync(_tenantA, branchId, Arg.Any<CancellationToken>()).Returns(Branch.Create(branchId, _tenantA, "Main", "MAIN", "UTC"));
        var inactive = MembershipPlan.Create(planId, _tenantA, "Student", null, 30, MembershipType.Custom, null, 10m, "USD", PackageCategory.Student);
        inactive.Deactivate();
        memberships.GetPlanAsync(_tenantA, planId, Arg.Any<CancellationToken>()).Returns(inactive);
        var handler = CreateMembershipHandler(people, branches, memberships);

        var denied = await handler.Handle(new CreateMembershipCommand(memberId, branchId, planId, Today, false, null), CancellationToken.None);
        denied.Error.Should().Be(GymErrors.InvalidState);
        await memberships.DidNotReceive().AddMembershipAsync(Arg.Any<GymMembership>(), Arg.Any<CancellationToken>());

        memberships.GetPlanAsync(_tenantA, planId, Arg.Any<CancellationToken>()).Returns((MembershipPlan?)null);
        var missing = await handler.Handle(new CreateMembershipCommand(memberId, branchId, planId, Today, false, null), CancellationToken.None);
        missing.Error.Should().Be(GymErrors.NotFound);
        await memberships.Received().GetPlanAsync(_tenantA, planId, Arg.Any<CancellationToken>());
        await memberships.DidNotReceive().GetPlanAsync(_tenantB, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PermissionAndFeatureDenials_DoNotQueryMemberships()
    {
        var memberships = Substitute.For<IMembershipRepository>();
        var handler = new GetPlanQueryHandler(Guard(GymErrors.FeatureDisabled), Actor(), memberships);
        var denied = await handler.Handle(new GetPlanQuery(Guid.NewGuid()), CancellationToken.None);
        denied.Error.Should().Be(GymErrors.FeatureDisabled);
        var forbidden = new ListMembershipsQueryHandler(Guard(IdentityErrors.Forbidden), Actor(), memberships, Clock());
        (await forbidden.Handle(new ListMembershipsQuery(null, null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
        await memberships.DidNotReceive().GetPlanAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await memberships.DidNotReceive().SearchMembershipsAsync(Arg.Any<Guid>(), Arg.Any<GymMembershipStatus?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly>(), Arg.Any<string?>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendedWorkspace_StopsTheMembershipRoute()
    {
        var tenant = Tenant.Create(_tenantA, "North Gym", TenantType.Gym);
        tenant.Suspend();
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(_tenantA, Arg.Any<CancellationToken>()).Returns(tenant);
        var called = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/memberships";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", _tenantA.ToString())], "test"));

        await middleware.InvokeAsync(context, tenants, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())));

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private CreateMembershipCommandHandler CreateMembershipHandler(IPeopleRepository people, IBranchRepository branches, IMembershipRepository memberships)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), PermissionCodes.LicenseManage, Arg.Any<CancellationToken>()).Returns(true);
        var unit = Substitute.For<IUnitOfWork>();
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));
        var ledger = Substitute.For<LifeFitAI.Application.Gym.Ledger.ILedgerPostingService>();
        ledger.TryPostMembershipPaymentAsync(Arg.Any<Guid>(), Arg.Any<LifeFitAI.Domain.Gym.MembershipPayment>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return new CreateMembershipCommandHandler(
            Guard(null),
            Actor(),
            permissions,
            people,
            branches,
            memberships,
            Substitute.For<LifeFitAI.Application.Gym.Payments.IMembershipPaymentRepository>(),
            ledger,
            Substitute.For<LifeFitAI.Application.Gym.Configuration.IBusinessConfigRepository>(),
            Substitute.For<IMemberAppSeatAssigner>(),
            Substitute.For<IAuditLogRepository>(),
            unit,
            Clock(),
            Substitute.For<IClientContext>());
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
        actor.UserId.Returns(Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1"));
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }

    private static UserAccount User(string email, string phone, string first, string last, Guid tenant) =>
        UserAccount.Create(Guid.NewGuid(), tenant, email, phone, "hash", "salt", first, last);

    private static void Stamp(ApplicationDbContext db, params LifeFitAI.SharedKernel.Primitives.BaseEntity[] entities)
    {
        foreach (var entity in entities)
        {
            db.Entry(entity).Property("RowVersion").CurrentValue = new byte[] { 1 };
        }
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
