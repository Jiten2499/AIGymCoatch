using System.Security.Claims;
using FluentAssertions;
using LifeFitAI.Api.Middleware;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Application.Gym.Ledger;
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

public sealed class ExpenseFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 6, 20, 0, 0, DateTimeKind.Utc);
    private readonly DateOnly _localToday = new(2026, 10, 7);

    [Fact]
    public void DatesAndAmounts_FollowTheExpenseRules()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST");
        ExpenseCalendar.LocalDate(_utc, zone).Should().Be(_localToday);
        ExpenseCalendar.WeekStart(_localToday).Should().Be(new DateOnly(2026, 10, 5));
        ExpenseCalendar.MonthStart(_localToday).Should().Be(new DateOnly(2026, 10, 1));
        ExpenseRules.Amount(0).Should().BeFalse();
        ExpenseRules.Amount(15_000m).Should().BeTrue();
        ExpenseRules.Date(_localToday.AddDays(7), _localToday).Should().BeTrue();
        ExpenseRules.Date(_localToday.AddDays(8), _localToday).Should().BeFalse();
        ExpenseRules.Date(new DateOnly(1999, 12, 31), _localToday).Should().BeFalse();
        var expense = Expense.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), "Rent", 100m, "INR", _localToday, ExpensePaymentMethod.Cash, _userA, null);
        expense.RecordedByUserId.Should().Be(_userA);
        expense.Void(_userA, _utc).Should().BeTrue();
        expense.Update(expense.CategoryId, "Rent", 100m, _localToday, ExpensePaymentMethod.Cash, null).Should().BeFalse();
    }

    [Fact]
    public async Task TenantIsolation_SearchSummaryAndHistory()
    {
        await using var db = CreateDb();
        var repo = new ExpenseRepository(db);
        var rent = ExpenseCategory.Create(Guid.NewGuid(), _tenantA, "Rent", null);
        var other = ExpenseCategory.Create(Guid.NewGuid(), _tenantB, "Rent", null);
        var today = Expense.Create(Guid.NewGuid(), _tenantA, rent.Id, "October rent", 10m, "INR", _localToday, ExpensePaymentMethod.BankTransfer, _userA, null);
        var yesterday = Expense.Create(Guid.NewGuid(), _tenantA, rent.Id, "Power", 5m, "INR", new DateOnly(2026, 10, 6), ExpensePaymentMethod.Cash, _userA, null);
        var previousMonth = Expense.Create(Guid.NewGuid(), _tenantA, rent.Id, "September", 100m, "INR", new DateOnly(2026, 9, 30), ExpensePaymentMethod.Cash, _userA, null);
        var voided = Expense.Create(Guid.NewGuid(), _tenantA, rent.Id, "Duplicate", 50m, "INR", _localToday, ExpensePaymentMethod.Cash, _userA, null);
        voided.Void(_userA, _utc);
        var foreign = Expense.Create(Guid.NewGuid(), _tenantB, other.Id, "October rent", 80m, "INR", _localToday, ExpensePaymentMethod.Upi, Guid.NewGuid(), null);
        Stamp(db, rent, other, today, yesterday, previousMonth, voided, foreign);
        db.ExpenseCategories.AddRange(rent, other);
        db.Expenses.AddRange(today, yesterday, previousMonth, voided, foreign);
        await db.SaveChangesAsync();

        (await repo.GetAsync(_tenantB, today.Id, CancellationToken.None)).Should().BeNull();
        (await repo.ReadAsync(_tenantB, today.Id, CancellationToken.None)).Should().BeNull();
        (await repo.GetCategoryAsync(_tenantB, rent.Id, CancellationToken.None)).Should().BeNull();
        var page = await repo.SearchAsync(_tenantA, new ExpenseSearch("rent", rent.Id, ExpenseStatus.Active, ExpensePaymentMethod.BankTransfer, _userA, _localToday, _localToday, 1m, 20m, null, 1, 20), CancellationToken.None);
        page.TotalCount.Should().Be(1);
        page.Items[0].Id.Should().Be(today.Id);
        var first = await repo.SearchAsync(_tenantA, new ExpenseSearch(null, null, null, null, null, null, null, null, null, null, 1, 2), CancellationToken.None);
        first.TotalCount.Should().Be(4);
        first.Items.Should().HaveCount(2);
        var second = await repo.SearchAsync(_tenantA, new ExpenseSearch(null, null, null, null, null, null, null, null, null, null, 2, 2), CancellationToken.None);
        second.Items.Should().HaveCount(2);
        var summary = await repo.SummarizeAsync(_tenantA, _localToday, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6), "INR", CancellationToken.None);
        summary.TotalAmount.Should().Be(5m);
        summary.TodayAmount.Should().Be(10m);
        summary.MonthAmount.Should().Be(15m);
        summary.WeekAmount.Should().Be(15m);
        summary.Categories.Should().ContainSingle(item => item.Name == "Rent" && item.Amount == 5m && item.Count == 1);
        var all = await repo.SummarizeAsync(_tenantB, _localToday, null, null, "INR", CancellationToken.None);
        all.TotalAmount.Should().Be(80m);
        rent.Deactivate();
        (await repo.ReadAsync(_tenantA, today.Id, CancellationToken.None))!.Category.Should().Be("Rent");
    }

    [Fact]
    public async Task Handlers_RejectCrossTenantInactiveAndDeniedAccess()
    {
        var expenses = Substitute.For<IExpenseRepository>();
        var denied = new GetExpenseQueryHandler(Guard(GymErrors.FeatureDisabled), Actor(), expenses);
        (await denied.Handle(new GetExpenseQuery(Guid.NewGuid()), CancellationToken.None)).Error.Should().Be(GymErrors.FeatureDisabled);
        var forbidden = new GetExpensesQueryHandler(Guard(IdentityErrors.Forbidden), Actor(), expenses);
        (await forbidden.Handle(new GetExpensesQuery(null, null, null, null, null, null, null, null, null, 1, 20, null), CancellationToken.None)).Error.Should().Be(IdentityErrors.Forbidden);
        await expenses.DidNotReceive().ReadAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await expenses.DidNotReceive().SearchAsync(Arg.Any<Guid>(), Arg.Any<ExpenseSearch>(), Arg.Any<CancellationToken>());

        var inactive = ExpenseCategory.Create(Guid.NewGuid(), _tenantA, "Rent", null);
        inactive.Deactivate();
        expenses.GetCategoryAsync(_tenantA, inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);
        var profiles = Substitute.For<IBusinessProfileRepository>();
        profiles.GetAsync(_tenantA, Arg.Any<CancellationToken>()).Returns(BusinessProfile.Create(Guid.NewGuid(), _tenantA, "UTC", "INR"));
        var ledger = Substitute.For<ILedgerPostingService>();
        ledger.TryPostExpenseAsync(Arg.Any<Guid>(), Arg.Any<Expense>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        ledger.TryReverseExpenseAsync(Arg.Any<Guid>(), Arg.Any<Expense>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var create = new CreateExpenseCommandHandler(Guard(null), Actor(), expenses, ledger, profiles, Audit(), Unit(), Clock(), Client());
        var blocked = await create.Handle(new CreateExpenseCommand(inactive.Id, "Rent", 10m, new DateOnly(2026, 10, 6), ExpensePaymentMethod.Cash, null), CancellationToken.None);
        blocked.Error.Should().Be(GymErrors.InvalidState);
        await expenses.DidNotReceive().AddAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>());

        var active = ExpenseCategory.Create(Guid.NewGuid(), _tenantA, "Electricity", null);
        expenses.GetCategoryAsync(_tenantA, active.Id, Arg.Any<CancellationToken>()).Returns(active);
        expenses.GetCategoryAsync(_tenantA, Guid.Empty, Arg.Any<CancellationToken>()).Returns((ExpenseCategory?)null);
        Expense? recorded = null;
        expenses.AddAsync(Arg.Do<Expense>(expense => recorded = expense), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        expenses.ReadAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new ExpenseDetails(Guid.NewGuid(), active.Id, "Electricity", "Bill", 10m, "INR", new DateOnly(2026, 10, 6), ExpensePaymentMethod.Upi, _userA, "Gym Owner", null, ExpenseStatus.Active, _utc, null));
        var created = await create.Handle(new CreateExpenseCommand(active.Id, "Bill", 10m, new DateOnly(2026, 10, 6), ExpensePaymentMethod.Upi, null), CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        recorded!.RecordedByUserId.Should().Be(_userA);
        recorded.Currency.Should().Be("INR");
        var missing = await create.Handle(new CreateExpenseCommand(Guid.Empty, "Bill", 10m, new DateOnly(2026, 10, 6), ExpensePaymentMethod.Cash, null), CancellationToken.None);
        missing.Error.Should().Be(GymErrors.NotFound);

        expenses.GetAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Expense?)null);
        var update = new UpdateExpenseCommandHandler(Guard(null), Actor(), expenses, profiles, Audit(), Unit(), Clock(), Client());
        (await update.Handle(new UpdateExpenseCommand(Guid.NewGuid(), active.Id, "Bill", 10m, new DateOnly(2026, 10, 6), ExpensePaymentMethod.Cash, null), CancellationToken.None)).Error.Should().Be(GymErrors.NotFound);
        var voided = new VoidExpenseCommandHandler(Guard(null), Actor(), expenses, ledger, Audit(), Unit(), Clock(), Client());
        (await voided.Handle(new VoidExpenseCommand(Guid.NewGuid()), CancellationToken.None)).Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task SuspendedWorkspace_StopsTheExpenseRoute()
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
        context.Request.Path = "/api/v1/expenses";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", _tenantA.ToString())], "test"));
        await middleware.InvokeAsync(context, tenants, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())));
        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
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
