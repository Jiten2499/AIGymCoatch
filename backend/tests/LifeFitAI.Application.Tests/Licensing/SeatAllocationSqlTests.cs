using FluentAssertions;
using LifeFitAI.Application.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Persistence.Interceptors;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Application.Tests.Licensing;

public sealed class SeatAllocationSqlTests
{
    [SqlFact]
    public async Task One_seat_allows_one_of_two_independent_contexts()
    {
        var fixture = await SeatFixture.CreateAsync(1);
        try
        {
            var first = fixture.AssignAsync(fixture.UserA);
            var second = fixture.AssignAsync(fixture.UserB);
            var results = await Task.WhenAll(first, second);
            results.Count(result => result == "ok").Should().Be(1);
            results.Count(result => result == "license.capacity_conflict").Should().Be(1);
            (await fixture.ActiveCountAsync()).Should().Be(1);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }

    [SqlFact]
    public async Task Update_lock_blocks_another_context_until_release()
    {
        var fixture = await SeatFixture.CreateAsync(1);
        try
        {
            await using var holder = fixture.Open();
            await using var transaction = await holder.Database.BeginTransactionAsync();
            await new LicenseAdminStore(holder).LockSubscriptionAsync(fixture.SubscriptionId, CancellationToken.None);
            var blocked = fixture.AssignAsync(fixture.UserA);
            await Task.Delay(750);
            blocked.IsCompleted.Should().BeFalse();
            await transaction.RollbackAsync();
            (await blocked).Should().Be("ok");
            (await fixture.ActiveCountAsync()).Should().Be(1);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }

    [SqlFact]
    public async Task Two_seats_allow_two_independent_assignments()
    {
        var fixture = await SeatFixture.CreateAsync(2);
        try
        {
            var results = await Task.WhenAll(fixture.AssignAsync(fixture.UserA), fixture.AssignAsync(fixture.UserB));
            results.Should().AllBe("ok");
            (await fixture.ActiveCountAsync()).Should().Be(2);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }

    [SqlFact]
    public async Task Second_active_assignment_for_the_same_user_is_rejected()
    {
        var fixture = await SeatFixture.CreateAsync(2);
        try
        {
            (await fixture.AssignAsync(fixture.UserA)).Should().Be("ok");
            (await fixture.AssignAsync(fixture.UserA)).Should().Be("license.assignment_conflict");
            (await fixture.ActiveCountAsync()).Should().Be(1);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }

    [SqlFact]
    public async Task Revoked_seat_can_be_assigned_to_someone_else()
    {
        var fixture = await SeatFixture.CreateAsync(1);
        try
        {
            (await fixture.AssignAsync(fixture.UserA)).Should().Be("ok");
            await fixture.RevokeActiveAsync();
            (await fixture.AssignAsync(fixture.UserB)).Should().Be("ok");
            (await fixture.ActiveCountAsync()).Should().Be(1);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }

    [SqlFact]
    public async Task Expired_license_rejects_assignment()
    {
        var fixture = await SeatFixture.CreateAsync(1);
        try
        {
            await fixture.ExpireAsync();
            (await fixture.AssignAsync(fixture.UserA)).Should().Be("license.not_found");
            (await fixture.ActiveCountAsync()).Should().Be(0);
        }
        finally
        {
            await fixture.DeleteAsync();
        }
    }
}

internal sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIFEFIT_SQL_CONNECTION")))
        {
            Skip = "Set LIFEFIT_SQL_CONNECTION to run SQL Server licensing tests.";
        }
    }
}

internal sealed class SeatFixture
{
    // BUSINESS_FREE plan seeded by changeset LF-0026.
    private static readonly Guid PlanId = Guid.Parse("a1111111-1111-4111-8111-111111111102");

    private SeatFixture(Guid tenantId, Guid subscriptionId, Guid userA, Guid userB)
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        UserA = userA;
        UserB = userB;
    }

    public Guid TenantId { get; }

    public Guid SubscriptionId { get; }

    public Guid UserA { get; }

    public Guid UserB { get; }

    public static async Task<SeatFixture> CreateAsync(int capacity)
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
SET NOCOUNT ON;
INSERT INTO app.Tenants (Id, Name, TenantType, Status, CreatedAt, UpdatedAt)
VALUES ({tenantId}, N'Seat test', N'Gym', N'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
INSERT INTO app.Users (Id, TenantId, Email, PasswordHash, PasswordSalt, FirstName, LastName, Status, CreatedAt, UpdatedAt)
VALUES ({userA}, {tenantId}, {userA.ToString("N") + "@seat.test"}, N'x', N'y', N'A', N'User', N'Active', SYSUTCDATETIME(), SYSUTCDATETIME()),
       ({userB}, {tenantId}, {userB.ToString("N") + "@seat.test"}, N'x', N'y', N'B', N'User', N'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
INSERT INTO app.Subscriptions (Id, SubscriberType, TenantId, PlanId, Status, StartsAt, Seats, CreatedAt, UpdatedAt)
VALUES ({subscriptionId}, N'Tenant', {tenantId}, {PlanId}, N'Active', SYSUTCDATETIME(), {capacity}, SYSUTCDATETIME(), SYSUTCDATETIME());");
        return new SeatFixture(tenantId, subscriptionId, userA, userB);
    }

    public ApplicationDbContext Open() => CreateContext();

    public async Task<string> AssignAsync(Guid userId)
    {
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reservation = await new LicenseAdminStore(db).ReserveSeatAsync(TenantId, SubscriptionId, userId, null, DateTime.UtcNow, CancellationToken.None);
        if (!reservation.Succeeded)
        {
            await transaction.RollbackAsync();
            return reservation.Error!.Code;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return "ok";
    }

    public async Task RevokeActiveAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.SubscriptionSeats SET Status = N'Released', ReleasedAt = SYSUTCDATETIME() WHERE SubscriptionId = {SubscriptionId} AND Status = N'Active';");
    }

    public async Task ExpireAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.Subscriptions SET ExpiresAt = DATEADD(minute, -5, SYSUTCDATETIME()) WHERE Id = {SubscriptionId};");
    }

    public async Task<int> ActiveCountAsync()
    {
        await using var db = CreateContext();
        return await db.SubscriptionSeats.CountAsync(seat => seat.SubscriptionId == SubscriptionId && seat.Status == Domain.Identity.SeatStatus.Active);
    }

    public async Task DeleteAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
SET NOCOUNT ON;
DELETE FROM app.SubscriptionSeats WHERE SubscriptionId = {SubscriptionId};
DELETE FROM app.Subscriptions WHERE Id = {SubscriptionId};
DELETE FROM app.Users WHERE TenantId = {TenantId};
DELETE FROM app.Tenants WHERE Id = {TenantId};");
    }

    private static ApplicationDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable("LIFEFIT_SQL_CONNECTION");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .AddInterceptors(new AuditableEntityInterceptor(new UtcClock(), new EmptyActor()))
            .Options;
        return new ApplicationDbContext(options);
    }
}

internal sealed class UtcClock : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

internal sealed class EmptyActor : ICurrentActor
{
    public Guid? UserId => null;

    public Guid? TenantId => null;
}
