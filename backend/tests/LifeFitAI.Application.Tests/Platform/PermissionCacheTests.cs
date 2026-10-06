using FluentAssertions;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeFitAI.Application.Tests.Platform;

public sealed class PermissionCacheTests
{
    private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private readonly Guid _userId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private readonly Guid _roleId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private readonly Guid _permissionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");

    [Fact]
    public async Task RoleAssignment_InvalidatesOnlyAfterCommit()
    {
        await using var scope = CreateScope();
        await SeedPermissionAsync(scope);
        await RememberAsync(scope, _tenantA, _userId, "OLD");
        await RememberAsync(scope, _tenantB, _userId, "OTHER");

        await scope.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), _userId, _roleId, _tenantA), CancellationToken.None);
        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantA, _userId), CancellationToken.None)).Should().Equal("OLD");

        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantA, _userId), CancellationToken.None)).Should().BeNull();
        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantB, _userId), CancellationToken.None)).Should().Equal("OTHER");
    }

    [Fact]
    public async Task RoleRemoval_InvalidatesOnlyAfterCommit()
    {
        await using var scope = CreateScope();
        await SeedPermissionAsync(scope);
        await scope.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), _userId, _roleId, _tenantA), CancellationToken.None);
        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        await RememberAsync(scope, _tenantA, _userId, PermissionCodes.MemberUpdate);

        await scope.Roles.RemoveForMemberAsync(_userId, _tenantA, DateTime.UtcNow, CancellationToken.None);
        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantA, _userId), CancellationToken.None)).Should().Equal(PermissionCodes.MemberUpdate);

        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantA, _userId), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task FailedSave_LeavesThePermissionCache()
    {
        await using var scope = CreateScope(failSave: true);
        await RememberAsync(scope, _tenantA, _userId, PermissionCodes.MemberUpdate);
        await scope.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), _userId, _roleId, _tenantA), CancellationToken.None);

        var act = () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await scope.Cache.GetAsync<string[]>(TenantCache.Permissions(_tenantA, _userId), CancellationToken.None)).Should().Equal(PermissionCodes.MemberUpdate);
    }

    [Fact]
    public async Task Revocation_IsVisibleOnTheNextPermissionCheck()
    {
        await using var scope = CreateScope();
        await SeedPermissionAsync(scope);
        scope.Db.TenantMembers.Add(TenantMember.Create(Guid.NewGuid(), _tenantA, _userId, DateTime.UtcNow));
        await scope.Roles.AddAsync(UserRole.Create(Guid.NewGuid(), _userId, _roleId, _tenantA), CancellationToken.None);
        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        var permissions = new CachingPermissionService(new TenantMembershipRepository(scope.Db), scope.Roles, scope.Cache);

        (await permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.MemberUpdate, CancellationToken.None)).Should().BeTrue();

        await scope.Roles.RemoveForMemberAsync(_userId, _tenantA, DateTime.UtcNow, CancellationToken.None);
        (await permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.MemberUpdate, CancellationToken.None)).Should().BeTrue();

        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        (await permissions.HasPermissionAsync(_userId, _tenantA, PermissionCodes.MemberUpdate, CancellationToken.None)).Should().BeFalse();
    }

    private async Task SeedPermissionAsync(CacheScope scope)
    {
        scope.Db.Roles.Add(Role.Create(_roleId, RoleNames.Member, null));
        scope.Db.Permissions.Add(Permission.Create(_permissionId, PermissionCodes.MemberUpdate, "Update member", null));
        scope.Db.RolePermissions.Add(RolePermission.Create(_roleId, _permissionId));
        await scope.Db.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task RememberAsync(CacheScope scope, Guid tenantId, Guid userId, params string[] codes) =>
        await scope.Cache.SetAsync(TenantCache.Permissions(tenantId, userId), codes, TimeSpan.FromSeconds(45), CancellationToken.None);

    private static CacheScope CreateScope(bool failSave = false)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new RowVersionInterceptor());
        if (failSave)
        {
            builder.AddInterceptors(new FailSaveInterceptor());
        }

        var db = new ApplicationDbContext(builder.Options);
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        var invalidator = new PermissionCacheInvalidator(cache, NullLogger<PermissionCacheInvalidator>.Instance);
        return new CacheScope(db, cache, new RoleRepository(db, invalidator), new UnitOfWork(db, invalidator));
    }

    private sealed class CacheScope : IAsyncDisposable
    {
        public CacheScope(ApplicationDbContext db, MemoryCacheService cache, RoleRepository roles, UnitOfWork unitOfWork)
        {
            Db = db;
            Cache = cache;
            Roles = roles;
            UnitOfWork = unitOfWork;
        }

        public ApplicationDbContext Db { get; }

        public MemoryCacheService Cache { get; }

        public RoleRepository Roles { get; }

        public UnitOfWork UnitOfWork { get; }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
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

    private sealed class FailSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("save failed");
    }
}
