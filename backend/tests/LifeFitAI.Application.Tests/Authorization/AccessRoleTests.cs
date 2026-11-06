using FluentAssertions;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace LifeFitAI.Application.Tests.Authorization;

public sealed class AccessRoleTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa10");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbb10");
    private readonly Guid _actor = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc10");
    private readonly Guid _staff = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddd10");

    [Fact]
    public void Rules_ProtectTheOwner_AndRejectEscalation()
    {
        AccessRoleRules.OwnerAssignmentAllowed(RoleNames.GymOwner, [RoleNames.GymManager]).Should().BeFalse();
        AccessRoleRules.OwnerAssignmentAllowed(RoleNames.GymOwner, [RoleNames.GymOwner]).Should().BeTrue();
        AccessRoleRules.CanGrant([PermissionCodes.MemberRead], [PermissionCodes.MemberRead]).Should().BeTrue();
        AccessRoleRules.CanGrant([PermissionCodes.MemberRead], [PermissionCodes.UserDelete]).Should().BeFalse();
        AccessScopes.MostPermissive([AccessScopes.Own, AccessScopes.Assigned, AccessScopes.All]).Should().Be(AccessScopes.All);
        Role.Create(Guid.NewGuid(), RoleNames.GymOwner, "Owner").TryRename("Other", null).Should().BeFalse();
        Role.Create(Guid.NewGuid(), RoleNames.GymOwner, "Owner").TrySetActive(false).Should().BeFalse();
    }

    [Fact]
    public async Task InactiveAndForeignRoles_DoNotGrantPermissions()
    {
        await using var db = CreateDb();
        var permission = Permission.Create(Guid.NewGuid(), PermissionCodes.MemberRead, "Read", null);
        var active = Role.CreateCustom(Guid.NewGuid(), _tenantA, "Floor", null);
        var inactive = Role.CreateCustom(Guid.NewGuid(), _tenantA, "Old", null);
        inactive.TrySetActive(false);
        var foreign = Role.CreateCustom(Guid.NewGuid(), _tenantB, "Other gym", null);
        db.Permissions.Add(permission);
        db.Roles.AddRange(active, inactive, foreign);
        db.RolePermissions.AddRange(
            RolePermission.Create(active.Id, permission.Id),
            RolePermission.Create(inactive.Id, permission.Id),
            RolePermission.Create(foreign.Id, permission.Id));
        db.UserRoles.AddRange(
            UserRole.Create(Guid.NewGuid(), _actor, active.Id, _tenantA),
            UserRole.Create(Guid.NewGuid(), _actor, inactive.Id, _tenantA),
            UserRole.Create(Guid.NewGuid(), _actor, foreign.Id, _tenantB));
        Stamp(db);
        await db.SaveChangesAsync();
        var store = new AccessRoleStore(db, Substitute.For<IPermissionCacheInvalidator>());
        var codes = await new RoleRepository(db, Substitute.For<IPermissionCacheInvalidator>()).GetPermissionCodesAsync(_actor, _tenantA, CancellationToken.None);

        codes.Should().Equal(PermissionCodes.MemberRead);
        (await store.FindVisibleAsync(_tenantA, foreign.Id, CancellationToken.None)).Should().BeNull();
        (await store.FindVisibleAsync(_tenantB, foreign.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task CustomRole_RejectsUnheldPermissions_AndAssignmentDoesNotConsumeASeat()
    {
        await using var db = CreateDb();
        var read = Permission.Create(Guid.NewGuid(), PermissionCodes.MemberRead, "Read", null);
        var delete = Permission.Create(Guid.NewGuid(), PermissionCodes.UserDelete, "Delete", null);
        var owner = Role.Create(Guid.NewGuid(), RoleNames.GymOwner, "Owner");
        db.AddRange(read, delete, owner, RolePermission.Create(owner.Id, read.Id));
        db.Users.Add(UserAccount.Create(_actor, _tenantA, "owner@gym.test", "+15550001001", "hash", "salt", "Ava", "Owner"));
        db.Users.Add(UserAccount.Create(_staff, _tenantA, "staff@gym.test", "+15550001002", "hash", "salt", "Sam", "Staff"));
        db.Tenants.Add(Tenant.Create(_tenantA, "Gym", TenantType.Gym));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenantA, _actor, DateTime.UtcNow));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenantA, _staff, DateTime.UtcNow));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), _actor, owner.Id, _tenantA));
        var seat = SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), _staff, _actor, DateTime.UtcNow);
        db.SubscriptionSeats.Add(seat);
        Stamp(db);
        await db.SaveChangesAsync();

        var cache = new PermissionCacheInvalidator(new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())), Substitute.For<Microsoft.Extensions.Logging.ILogger<PermissionCacheInvalidator>>());
        var store = new AccessRoleStore(db, cache);
        var roles = new RoleRepository(db, cache);
        var handler = new AccessRoleCommandHandler(Guard(), Actor(), roles, store, cache, Substitute.For<IAuditLogRepository>(), new UnitOfWork(db, cache), Clock(), Substitute.For<IClientContext>());
        var escalated = await handler.Handle(new CreateAccessRoleCommand("Front desk", null, [PermissionCodes.UserDelete], []), CancellationToken.None);
        var created = await handler.Handle(new CreateAccessRoleCommand("Front desk", "Desk", [PermissionCodes.MemberRead], [new AccessScopeInput("members", AccessScopes.Assigned)]), CancellationToken.None);
        escalated.IsFailure.Should().BeTrue();
        escalated.Error.Should().Be(IdentityErrors.RoleEscalation);
        created.IsSuccess.Should().BeTrue();

        var protectedUpdate = await handler.Handle(new UpdateAccessRoleCommand(owner.Id, "Owner", null, [PermissionCodes.MemberRead], []), CancellationToken.None);
        protectedUpdate.Error.Should().Be(IdentityErrors.RoleProtected);

        var assign = new AccessRoleLifecycleHandler(Guard(), Actor(), roles, store, Tenants(db), Members(db), cache, Substitute.For<IAuditLogRepository>(), new UnitOfWork(db, cache), Clock(), Substitute.For<IClientContext>());
        var assigned = await assign.Handle(new AssignAccessRoleCommand(_staff, created.Value.Id), CancellationToken.None);
        assigned.IsSuccess.Should().BeTrue();
        db.SubscriptionSeats.Count(item => item.UserId == _staff && item.Status == SeatStatus.Active).Should().Be(1);
        (await roles.GetRoleNamesAsync(_staff, _tenantA, CancellationToken.None)).Should().Contain("Front desk");
    }

    [Fact]
    public async Task PermissionCache_RefreshesAfterARoleChange()
    {
        await using var db = CreateDb();
        var permission = Permission.Create(Guid.NewGuid(), PermissionCodes.MemberRead, "Read", null);
        var role = Role.CreateCustom(Guid.NewGuid(), _tenantA, "Coach", null);
        db.AddRange(permission, role, RolePermission.Create(role.Id, permission.Id));
        db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenantA, _staff, DateTime.UtcNow));
        db.UserRoles.Add(UserRole.Create(Guid.NewGuid(), _staff, role.Id, _tenantA));
        Stamp(db);
        await db.SaveChangesAsync();
        var memory = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        var invalidator = new PermissionCacheInvalidator(memory, Substitute.For<Microsoft.Extensions.Logging.ILogger<PermissionCacheInvalidator>>());
        var roles = new RoleRepository(db, invalidator);
        var permissions = new CachingPermissionService(Members(db), roles, memory);
        (await permissions.HasPermissionAsync(_staff, _tenantA, PermissionCodes.MemberRead, CancellationToken.None)).Should().BeTrue();

        role.TrySetActive(false);
        invalidator.Schedule(_tenantA, _staff);
        await new UnitOfWork(db, invalidator).SaveChangesAsync();

        (await permissions.HasPermissionAsync(_staff, _tenantA, PermissionCodes.MemberRead, CancellationToken.None)).Should().BeFalse();
    }

    private IOperationGuard Guard()
    {
        var guard = Substitute.For<IOperationGuard>();
        guard.AllowAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return guard;
    }

    private ICurrentActor Actor()
    {
        var actor = Substitute.For<ICurrentActor>();
        actor.UserId.Returns(_actor);
        actor.TenantId.Returns(_tenantA);
        return actor;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
        return clock;
    }

    private static ITenantRepository Tenants(ApplicationDbContext db)
    {
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => db.Tenants.FirstOrDefault(tenant => tenant.Id == call.Arg<Guid>()));
        return tenants;
    }

    private static ITenantMembershipRepository Members(ApplicationDbContext db)
    {
        var members = Substitute.For<ITenantMembershipRepository>();
        members.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => db.TenantMembers.FirstOrDefault(member => member.TenantId == call.ArgAt<Guid>(0) && member.UserId == call.ArgAt<Guid>(1)));
        return members;
    }

    private static void Stamp(ApplicationDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(entry => entry.Metadata.FindProperty("RowVersion") is not null))
        {
            entry.Property("RowVersion").CurrentValue ??= new byte[] { 1 };
        }
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options);
        db.SavingChanges += (_, _) => Stamp(db);
        return db;
    }
}
