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

public sealed class TenantStatusCacheTests
{
    private readonly Guid _tenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly Guid _tenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task Suspension_InvalidatesStatusOnlyAfterCommit()
    {
        await using var scope = CreateScope();
        var tenant = Tenant.Create(_tenantA, "North", TenantType.Gym);
        scope.Db.Tenants.Add(tenant);
        await scope.Db.SaveChangesAsync(CancellationToken.None);
        await RememberAsync(scope, _tenantA, TenantStatus.Active.ToString());
        await RememberAsync(scope, _tenantB, TenantStatus.Active.ToString());

        tenant.Suspend();
        scope.Invalidator.ScheduleStatus(_tenantA);
        (await scope.Cache.GetAsync<string>(TenantCache.Status(_tenantA), CancellationToken.None)).Should().Be(TenantStatus.Active.ToString());

        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        (await scope.Cache.GetAsync<string>(TenantCache.Status(_tenantA), CancellationToken.None)).Should().BeNull();
        (await scope.Cache.GetAsync<string>(TenantCache.Status(_tenantB), CancellationToken.None)).Should().Be(TenantStatus.Active.ToString());
        (await CurrentStatusAsync(scope, _tenantA)).Should().Be(TenantStatus.Suspended.ToString());
    }

    [Fact]
    public async Task FailedSave_LeavesTheStatusCache()
    {
        await using var scope = CreateScope(failSave: true);
        await RememberAsync(scope, _tenantA, TenantStatus.Active.ToString());
        scope.Invalidator.ScheduleStatus(_tenantA);

        var act = () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await scope.Cache.GetAsync<string>(TenantCache.Status(_tenantA), CancellationToken.None)).Should().Be(TenantStatus.Active.ToString());
    }

    private static async Task<string> CurrentStatusAsync(CacheScope scope, Guid tenantId)
    {
        var cached = await scope.Cache.GetAsync<string>(TenantCache.Status(tenantId), CancellationToken.None);
        if (cached is not null)
        {
            return cached;
        }

        var tenant = await scope.Db.Tenants.SingleAsync(item => item.Id == tenantId, CancellationToken.None);
        return tenant.Status.ToString();
    }

    private static Task RememberAsync(CacheScope scope, Guid tenantId, string status) =>
        scope.Cache.SetAsync(TenantCache.Status(tenantId), status, TimeSpan.FromSeconds(30), CancellationToken.None);

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
        return new CacheScope(db, cache, invalidator, new UnitOfWork(db, invalidator));
    }

    private sealed class CacheScope : IAsyncDisposable
    {
        public CacheScope(ApplicationDbContext db, MemoryCacheService cache, PermissionCacheInvalidator invalidator, UnitOfWork unitOfWork)
        {
            Db = db;
            Cache = cache;
            Invalidator = invalidator;
            UnitOfWork = unitOfWork;
        }

        public ApplicationDbContext Db { get; }

        public MemoryCacheService Cache { get; }

        public PermissionCacheInvalidator Invalidator { get; }

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
