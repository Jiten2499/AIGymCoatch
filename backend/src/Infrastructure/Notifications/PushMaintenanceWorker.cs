using LifeFitAI.Application.Notifications;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeFitAI.Infrastructure.Notifications;

public sealed class PushMaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PushMaintenanceWorker> _logger;

    public PushMaintenanceWorker(IServiceScopeFactory scopes, ILogger<PushMaintenanceWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Push maintenance failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var job = scope.ServiceProvider.GetRequiredService<PushDispatchJob>();
        var now = clock.UtcNow;
        await RemindDueWorkoutsAsync(db, now, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var cutoff = now.AddSeconds(-30);
        var since = now.AddDays(-2);
        var pending = await db.Notifications.AsNoTracking()
            .Where(item => item.CreatedAt <= cutoff && item.CreatedAt >= since && !db.PushDeliveries.Any(delivery => delivery.NotificationId == item.Id))
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Id)
            .Take(40)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var id in pending)
        {
            try
            {
                await job.DeliverAsync(id).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Push dispatch failed for notification {NotificationId}", id);
            }
        }

        await ConfirmReceiptsAsync(scope.ServiceProvider, db, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemindDueWorkoutsAsync(ApplicationDbContext db, DateTime utcNow, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        var windowStart = today.AddDays(-31);
        var programs = await db.WorkoutPrograms.AsNoTracking()
            .Where(item => item.TenantId != null && item.UserId != null && item.PlanStartDate != null && item.PlanStartDate >= windowStart && item.PlanStartDate <= today)
            .Select(item => new { item.Id, TenantId = item.TenantId!.Value, UserId = item.UserId!.Value, Start = item.PlanStartDate!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (programs.Count == 0)
        {
            return;
        }

        var ids = programs.Select(item => item.Id).ToArray();
        var days = await db.WorkoutProgramDays.AsNoTracking().Where(item => ids.Contains(item.WorkoutProgramId) && !item.RestDay).Select(item => new { item.WorkoutProgramId, item.DayNumber }).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var program in programs)
        {
            var due = days.Any(day => day.WorkoutProgramId == program.Id && program.Start.AddDays(day.DayNumber - 1) == today);
            if (!due)
            {
                continue;
            }

            var key = $"workout.reminder:{program.UserId:D}:{today:yyyy-MM-dd}";
            if (await db.Notifications.AnyAsync(item => item.TenantId == program.TenantId && item.UserId == program.UserId && item.DedupKey == key, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var notice = Notification.Create(Guid.NewGuid(), program.UserId, program.TenantId, NotificationType.WorkoutReminder, "Workout reminder", "You have a workout planned today.", utcNow);
            notice.Bind("WorkoutProgram", program.Id, key);
            db.Notifications.Add(notice);
        }
    }

    private async Task ConfirmReceiptsAsync(IServiceProvider services, ApplicationDbContext db, DateTime utcNow, CancellationToken cancellationToken)
    {
        var waiting = await db.PushDeliveries.Where(item => item.Status == PushDeliveryStatus.Accepted && item.TicketId != null && item.UpdatedAt <= utcNow.AddSeconds(-20)).OrderBy(item => item.UpdatedAt).Take(100).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (waiting.Count == 0)
        {
            return;
        }

        var receipts = await services.GetRequiredService<IExpoPushClient>().ReceiptsAsync(waiting.Select(item => item.TicketId!).Distinct().ToArray(), cancellationToken).ConfigureAwait(false);
        foreach (var delivery in waiting)
        {
            if (delivery.TicketId is null || !receipts.TryGetValue(delivery.TicketId, out var receipt))
            {
                continue;
            }

            if (receipt.Accepted)
            {
                delivery.Confirm(utcNow);
                continue;
            }

            delivery.Fail(receipt.Error ?? "ReceiptFailed", utcNow);
            if (ExpoReceipts.DeviceGone(receipt.Error))
            {
                var device = await db.PushDevices.FirstOrDefaultAsync(item => item.Id == delivery.DeviceId, cancellationToken).ConfigureAwait(false);
                device?.Disable(utcNow);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
