using FluentAssertions;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeFitAI.Application.Tests.Gym;

public sealed class PtPackageAssignmentTests
{
    private readonly Guid _tenant = Guid.NewGuid();

    [Fact]
    public void Membership_copies_included_sessions_without_a_package_sale()
    {
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenant, "Annual", "Pool access", 365, MembershipType.Yearly, null, 1200m, "USD", includedPtSessions: 4);
        var membership = GymMembership.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), null, plan.Id, "G00000001", new DateOnly(2026, 10, 9), plan.InclusiveEnd(new DateOnly(2026, 10, 9)), new DateOnly(2026, 10, 9), false, null);
        membership.ApplyIncludedSessions(plan.IncludedPtSessions);
        membership.IncludedPtSessions.Should().Be(4);
    }

    [Fact]
    public void Standalone_assignment_stays_unpaid_and_uses_the_package_balance()
    {
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "10 sessions", null, 10, 60, 300m, "USD");
        var assignment = MemberPtAssignment.Open(Guid.NewGuid(), package, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 9));
        assignment.PaymentStatus.Should().Be(PtPaymentStatus.Unpaid);
        assignment.SessionCount.Should().Be(10);
        assignment.SessionsRemaining.Should().Be(10);
        assignment.EndDate.Should().Be(new DateOnly(2026, 12, 7));
        assignment.TrainerProfileId.Should().NotBeNull();
    }

    [Fact]
    public async Task Inactive_or_unknown_package_is_rejected()
    {
        await using var db = CreateDb();
        var repository = new PtPackageRepository(db);
        var inactive = PtPackage.Create(Guid.NewGuid(), _tenant, "Old", null, 8, 30, 100m, "USD");
        inactive.Deactivate();
        await repository.AddAsync(inactive, CancellationToken.None);
        await db.SaveChangesAsync();

        var openInactive = () => MemberPtOpening.OpenAsync(repository, _tenant, Guid.NewGuid(), null, inactive.Id, new DateOnly(2026, 10, 9), CancellationToken.None);
        await openInactive.Should().ThrowAsync<MemberOnboardingException>();

        var missing = () => MemberPtOpening.OpenAsync(repository, _tenant, Guid.NewGuid(), null, Guid.NewGuid(), new DateOnly(2026, 10, 9), CancellationToken.None);
        await missing.Should().ThrowAsync<MemberOnboardingException>();
    }

    [Fact]
    public async Task Repeated_assignment_of_the_same_package_is_rejected()
    {
        await using var db = CreateDb();
        var repository = new PtPackageRepository(db);
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Starter", null, 6, 30, 80m, "USD");
        await repository.AddAsync(package, CancellationToken.None);
        await db.SaveChangesAsync();
        var memberId = Guid.NewGuid();
        await MemberPtOpening.OpenAsync(repository, _tenant, memberId, null, package.Id, new DateOnly(2026, 10, 9), CancellationToken.None);
        await db.SaveChangesAsync();

        var again = () => MemberPtOpening.OpenAsync(repository, _tenant, memberId, null, package.Id, new DateOnly(2026, 10, 9), CancellationToken.None);
        await again.Should().ThrowAsync<MemberOnboardingException>();
        (await db.MemberPtAssignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_package_from_another_tenant_is_not_visible()
    {
        await using var db = CreateDb();
        var repository = new PtPackageRepository(db);
        var package = PtPackage.Create(Guid.NewGuid(), Guid.NewGuid(), "Other gym", null, 4, 30, 50m, "USD");
        await repository.AddAsync(package, CancellationToken.None);
        await db.SaveChangesAsync();
        (await repository.GetAsync(_tenant, package.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void Assignment_keeps_the_package_snapshot_and_rejects_use_after_expiry()
    {
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Starter", null, 2, 30, 80m, "USD");
        var start = new DateOnly(2026, 10, 9);
        var assignment = MemberPtAssignment.Open(Guid.NewGuid(), package, Guid.NewGuid(), null, start);
        assignment.PackageName.Should().Be("Starter");
        assignment.ValidityDays.Should().Be(30);
        assignment.TryConsume(start).Should().BeTrue();
        assignment.TryConsume(start.AddDays(1)).Should().BeTrue();
        assignment.Status.Should().Be(MemberPtAssignmentStatus.Exhausted);
        assignment.Restore().Should().BeTrue();
        assignment.Status.Should().Be(MemberPtAssignmentStatus.Active);
        assignment.TryConsume(start.AddDays(40)).Should().BeFalse();
        assignment.Status.Should().Be(MemberPtAssignmentStatus.Expired);
    }

    [Fact]
    public void Membership_sessions_are_separate_from_a_package_and_can_be_reversed()
    {
        var plan = MembershipPlan.Create(Guid.NewGuid(), _tenant, "Monthly", null, 30, MembershipType.Monthly, null, 100m, "USD", includedPtSessions: 1);
        var start = new DateOnly(2026, 10, 9);
        var membership = GymMembership.Create(Guid.NewGuid(), _tenant, Guid.NewGuid(), null, plan.Id, "G00000002", start, plan.InclusiveEnd(start), start, false, null);
        membership.ApplyIncludedSessions(plan.IncludedPtSessions);
        membership.TryConsumeIncludedSession(start).Should().BeTrue();
        membership.PtSessionsRemaining.Should().Be(0);
        var completedAt = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var session = PtSession.Complete(Guid.NewGuid(), _tenant, membership.MemberProfileId, Guid.NewGuid(), start, completedAt, null);
        var consumption = PtSessionConsumption.ForMembership(Guid.NewGuid(), session, membership.Id, completedAt);
        consumption.Reverse(completedAt, "Booked in error").Should().BeTrue();
        membership.RestoreIncludedSession().Should().BeTrue();
        membership.PtSessionsRemaining.Should().Be(1);
        consumption.Reverse(completedAt, "Again").Should().BeFalse();
    }

    [Fact]
    public void Draft_package_is_not_active()
    {
        var package = PtPackage.Create(Guid.NewGuid(), _tenant, "Draft pack", null, 4, 30, 40m, "USD");
        package.MarkDraft();
        package.Status.Should().Be(PtPackageStatus.Draft);
        package.IsActive.Should().BeFalse();
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class RowVersionInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries())
            {
                var property = entry.Properties.FirstOrDefault(item => item.Metadata.Name == "RowVersion");
                if (property is not null && property.CurrentValue is null)
                {
                    property.CurrentValue = new byte[] { 1 };
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
