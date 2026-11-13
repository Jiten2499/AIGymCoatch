using FluentAssertions;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class LedgerFoundationTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private readonly DateTime _utc = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private readonly DateOnly _today = new(2026, 10, 7);

    [Fact]
    public void JournalEntry_RequiresBalancedLines()
    {
        var cash = Guid.NewGuid();
        var revenue = Guid.NewGuid();
        var act = () => JournalEntry.CreatePosted(
            Guid.NewGuid(), _tenantA, _today, "Payment", JournalEntryType.MoneyIn, JournalSourceType.MembershipPayment, Guid.NewGuid(),
            "Membership", cash, null, 100m, "INR", _userId, _utc, null,
            [(cash, 100m, 0m), (revenue, 0m, 90m)]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task FeatureDisabled_SkipsPostingAndBlocksApi()
    {
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(false);
        var ledger = Substitute.For<ILedgerRepository>();
        var service = new LedgerPostingService(features, ledger, Substitute.For<IBusinessProfileRepository>(), Unit(), Clock());
        var payment = MembershipPayment.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), 100m, "INR", MembershipPaymentMethod.Cash, _today, _userId, null, null);
        (await service.TryPostMembershipPaymentAsync(_tenantA, payment, _userId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        await ledger.DidNotReceive().AddEntryAsync(Arg.Any<JournalEntry>(), Arg.Any<IReadOnlyList<JournalEntryLine>>(), Arg.Any<CancellationToken>());

        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.LedgerRead, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(Result.Failure(GymErrors.FeatureDisabled));
        var queries = Queries(guard, Actor(_tenantA), ledger);
        var denied = await queries.Handle(new GetLedgerSummaryQuery(_today, _today), CancellationToken.None);
        denied.Error.Should().Be(GymErrors.FeatureDisabled);
    }

    [Fact]
    public async Task PostMembershipPayment_IsIdempotentAndTenantScoped()
    {
        var cashId = Guid.NewGuid();
        var cashLedgerId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();
        var cash = FinancialAccount.Create(cashId, _tenantA, "Cash", FinancialAccountKind.Cash, "INR", cashLedgerId);
        var revenue = LedgerAccount.Create(revenueId, _tenantA, LedgerAccountCodes.MembershipRevenue, "Membership Revenue", LedgerAccountClass.Revenue, true);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(true);
        var repo = Substitute.For<ILedgerRepository>();
        repo.SourceExistsAsync(_tenantA, JournalSourceType.MembershipPayment, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false, true);
        repo.FindActiveByKindAsync(_tenantA, FinancialAccountKind.Cash, Arg.Any<CancellationToken>()).Returns(cash);
        repo.GetLedgerAccountByCodeAsync(_tenantA, LedgerAccountCodes.MembershipRevenue, Arg.Any<CancellationToken>()).Returns(revenue);
        var service = new LedgerPostingService(features, repo, Substitute.For<IBusinessProfileRepository>(), Unit(), Clock());
        var payment = MembershipPayment.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), Guid.NewGuid(), 5000m, "INR", MembershipPaymentMethod.Cash, _today, _userId, null, null);
        (await service.TryPostMembershipPaymentAsync(_tenantA, payment, _userId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await service.TryPostMembershipPaymentAsync(_tenantA, payment, _userId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        await repo.Received(1).AddEntryAsync(Arg.Any<JournalEntry>(), Arg.Is<IReadOnlyList<JournalEntryLine>>(lines => lines.Count == 2 && lines.Sum(line => line.DebitAmount) == 5000m), Arg.Any<CancellationToken>());

        var actorB = Actor(_tenantB);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        repo.GetFinancialAccountAsync(_tenantB, cashId, Arg.Any<CancellationToken>()).Returns((FinancialAccount?)null);
        var balance = await Queries(guard, actorB, repo)
            .Handle(new GetFinancialAccountBalanceQuery(cashId), CancellationToken.None);
        balance.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task Transfer_RejectsForeignAccounts()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.LedgerCreate, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var repo = Substitute.For<ILedgerRepository>();
        repo.GetFinancialAccountAsync(_tenantA, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FinancialAccount?)null);
        var handler = new LedgerCommandHandler(guard, Actor(_tenantA), repo, Substitute.For<IBusinessProfileRepository>(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new CreateTransferCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, _today, null), CancellationToken.None);
        result.Error.Should().Be(GymErrors.NotFound);
    }

    [Fact]
    public async Task Transfer_PostsBalancedAssetMove()
    {
        var fromLedger = Guid.NewGuid();
        var toLedger = Guid.NewGuid();
        var from = FinancialAccount.Create(Guid.NewGuid(), _tenantA, "HDFC", FinancialAccountKind.Bank, "INR", fromLedger);
        var to = FinancialAccount.Create(Guid.NewGuid(), _tenantA, "Cash", FinancialAccountKind.Cash, "INR", toLedger);
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(PermissionCodes.LedgerCreate, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var repo = Substitute.For<ILedgerRepository>();
        repo.GetFinancialAccountAsync(_tenantA, from.Id, Arg.Any<CancellationToken>()).Returns(from);
        repo.GetFinancialAccountAsync(_tenantA, to.Id, Arg.Any<CancellationToken>()).Returns(to);
        var handler = new LedgerCommandHandler(guard, Actor(_tenantA), repo, Substitute.For<IBusinessProfileRepository>(), Substitute.For<IAuditLogRepository>(), Unit(), Clock(), Substitute.For<IClientContext>());
        var result = await handler.Handle(new CreateTransferCommand(from.Id, to.Id, 20000m, _today, null), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        await repo.Received(1).AddEntryAsync(
            Arg.Is<JournalEntry>(entry => entry.EntryType == JournalEntryType.Transfer && entry.Amount == 20000m),
            Arg.Is<IReadOnlyList<JournalEntryLine>>(lines => lines.Sum(line => line.DebitAmount) == 20000m && lines.Sum(line => line.CreditAmount) == 20000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpensePosting_DebitsExpenseCreditsMoneyAccount()
    {
        var cashId = Guid.NewGuid();
        var cashLedgerId = Guid.NewGuid();
        var expenseLedgerId = Guid.NewGuid();
        var cash = FinancialAccount.Create(cashId, _tenantA, "Cash", FinancialAccountKind.Cash, "INR", cashLedgerId);
        var expenseAccount = LedgerAccount.Create(expenseLedgerId, _tenantA, LedgerAccountCodes.OperatingExpense, "Operating Expense", LedgerAccountClass.Expense, true);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(true);
        var repo = Substitute.For<ILedgerRepository>();
        repo.SourceExistsAsync(_tenantA, JournalSourceType.Expense, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        repo.FindActiveByKindAsync(_tenantA, FinancialAccountKind.Cash, Arg.Any<CancellationToken>()).Returns(cash);
        repo.GetLedgerAccountByCodeAsync(_tenantA, LedgerAccountCodes.OperatingExpense, Arg.Any<CancellationToken>()).Returns(expenseAccount);
        var service = new LedgerPostingService(features, repo, Substitute.For<IBusinessProfileRepository>(), Unit(), Clock());
        var expense = Expense.Create(Guid.NewGuid(), _tenantA, Guid.NewGuid(), "Rent", 50000m, "INR", _today, ExpensePaymentMethod.Cash, _userId, null);
        (await service.TryPostExpenseAsync(_tenantA, expense, "Rent", _userId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        await repo.Received(1).AddEntryAsync(
            Arg.Is<JournalEntry>(entry => entry.EntryType == JournalEntryType.MoneyOut && entry.Category == "Rent"),
            Arg.Is<IReadOnlyList<JournalEntryLine>>(lines => lines.Any(line => line.LedgerAccountId == expenseLedgerId && line.DebitAmount == 50000m) && lines.Any(line => line.LedgerAccountId == cashLedgerId && line.CreditAmount == 50000m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsavedDefaultAccounts_AreVisibleToTheNextPost()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var repo = new LedgerRepository(db);
        await repo.EnsureDefaultsAsync(_tenantA, "INR", CancellationToken.None);

        var cash = await repo.FindActiveByKindAsync(_tenantA, FinancialAccountKind.Cash, CancellationToken.None);
        var revenue = await repo.GetLedgerAccountByCodeAsync(_tenantA, LedgerAccountCodes.MembershipRevenue, CancellationToken.None);
        cash.Should().NotBeNull();
        revenue.Should().NotBeNull();
        cash!.LedgerAccountId.Should().NotBe(Guid.Empty);

        await repo.EnsureDefaultsAsync(_tenantA, "INR", CancellationToken.None);
        db.FinancialAccounts.Local.Count(item => item.TenantId == _tenantA && item.AccountKind == FinancialAccountKind.Cash).Should().Be(1);
    }

    [Fact]
    public async Task PostPtPackagePayment_CreditsPersonalTrainingRevenue()
    {
        var cashLedgerId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();
        var cash = FinancialAccount.Create(Guid.NewGuid(), _tenantA, "Cash", FinancialAccountKind.Cash, "INR", cashLedgerId);
        var revenue = LedgerAccount.Create(revenueId, _tenantA, LedgerAccountCodes.PtRevenue, "Personal Training Revenue", LedgerAccountClass.Revenue, true);
        var features = Substitute.For<IFeatureFlagService>();
        features.IsEnabledAsync(_tenantA, FeatureCodes.GymFinancialLedger, Arg.Any<CancellationToken>()).Returns(true);
        var repo = Substitute.For<ILedgerRepository>();
        repo.SourceExistsAsync(_tenantA, JournalSourceType.PtPackagePayment, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false, true);
        repo.FindActiveByKindAsync(_tenantA, FinancialAccountKind.Cash, Arg.Any<CancellationToken>()).Returns(cash);
        repo.GetLedgerAccountByCodeAsync(_tenantA, LedgerAccountCodes.PtRevenue, Arg.Any<CancellationToken>()).Returns(revenue);
        var service = new LedgerPostingService(features, repo, Substitute.For<IBusinessProfileRepository>(), Unit(), Clock());
        var package = PtPackage.Create(Guid.NewGuid(), _tenantA, "Starter", null, 8, 30, 100m, "INR");
        var assignment = MemberPtAssignment.Open(Guid.NewGuid(), package, Guid.NewGuid(), null, _today);
        assignment.RecordOpeningPayment(80m, MembershipPaymentMethod.Cash, _today, null, null);

        (await service.TryPostPtPackagePaymentAsync(_tenantA, assignment, _userId, CancellationToken.None)).Value.Should().BeTrue();
        (await service.TryPostPtPackagePaymentAsync(_tenantA, assignment, _userId, CancellationToken.None)).Value.Should().BeFalse();

        await repo.Received(1).AddEntryAsync(
            Arg.Is<JournalEntry>(entry => entry.SourceType == JournalSourceType.PtPackagePayment && entry.EntryType == JournalEntryType.MoneyIn && entry.Amount == 80m && entry.Category == "Personal training"),
            Arg.Is<IReadOnlyList<JournalEntryLine>>(lines => lines.Any(line => line.LedgerAccountId == revenueId && line.CreditAmount == 80m) && lines.Any(line => line.LedgerAccountId == cashLedgerId && line.DebitAmount == 80m)),
            Arg.Any<CancellationToken>());
    }

    private static LedgerQueryHandler Queries(IOperationGuard guard, ICurrentActor actor, ILedgerRepository ledger)
    {
        var packages = Substitute.For<IPtPackageRepository>();
        packages.ListUnpostedPaidAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<MemberPtAssignment>());
        var posting = Substitute.For<ILedgerPostingService>();
        posting.PostOutstandingPtPaymentsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<MemberPtAssignment>>(), Arg.Any<CancellationToken>()).Returns(Result.Success(false));
        return new LedgerQueryHandler(guard, actor, ledger, Substitute.For<IBusinessProfileRepository>(), posting, packages);
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
