using LifeFitAI.Application.Notifications;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Notifications;

public sealed class PushStore : IPushStore
{
    private readonly ApplicationDbContext _db;

    public PushStore(ApplicationDbContext db) => _db = db;

    public Task<PushDevice?> FindInstallationAsync(Guid userId, string installationId, CancellationToken cancellationToken) =>
        _db.PushDevices.FirstOrDefaultAsync(item => item.UserId == userId && item.InstallationId == installationId, cancellationToken);

    public Task<PushDevice?> FindActiveTokenAsync(string token, CancellationToken cancellationToken) =>
        _db.PushDevices.FirstOrDefaultAsync(item => item.IsActive && item.ExpoPushToken == token, cancellationToken);

    public Task<int> CountActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.PushDevices.CountAsync(item => item.UserId == userId && item.IsActive, cancellationToken);

    public void Add(PushDevice device) => _db.PushDevices.Add(device);

    public Task<NotificationPreference?> FindPreferenceAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.NotificationPreferences.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken);

    public void Add(NotificationPreference preference) => _db.NotificationPreferences.Add(preference);

    public async Task MarkAllReadAsync(Guid tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = await _db.Notifications.Where(item => item.TenantId == tenantId && item.UserId == userId && !item.IsRead).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in unread)
        {
            item.MarkRead(utcNow);
        }
    }
}
