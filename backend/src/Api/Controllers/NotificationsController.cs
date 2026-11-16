using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Challenges;
using LifeFitAI.Application.Notifications;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    public NotificationsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("notifications")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListNotificationsQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("notifications/summary")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetNotificationSummaryQuery(), cancellationToken)).ToActionResult();

    [HttpGet("notifications/sync")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> Sync(DateTime? sinceUtc, int? take, CancellationToken cancellationToken) =>
        (await Sender.Send(new SyncNotificationsQuery(sinceUtc, take ?? 50), cancellationToken)).ToActionResult();

    [HttpPost("notifications/{id:guid}/read")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkNotificationReadCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("notifications/read-all")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken)).ToActionResult();

    [HttpPost("notifications/devices")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    [EnableRateLimiting("push")]
    public async Task<IActionResult> RegisterDevice(RegisterPushDeviceRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RegisterPushDeviceCommand(request.InstallationId, request.ExpoPushToken, request.Platform, request.AppVersion), cancellationToken)).ToActionResult();

    [HttpDelete("notifications/devices/{installationId}")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    [EnableRateLimiting("push")]
    public async Task<IActionResult> DeactivateDevice(string installationId, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeactivatePushDeviceCommand(installationId), cancellationToken)).ToActionResult();

    [HttpGet("notifications/preferences")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> Preferences(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetNotificationPreferencesQuery(), cancellationToken)).ToActionResult();

    [HttpPut("notifications/preferences")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    [EnableRateLimiting("push")]
    public async Task<IActionResult> UpdatePreferences(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateNotificationPreferencesCommand(request.ChatEnabled, request.WorkoutRemindersEnabled, request.WorkoutUpdatesEnabled, request.ProgressEnabled, request.AccountEnabled, request.SoundEnabled, request.VibrationEnabled), cancellationToken)).ToActionResult();
}

public sealed record RegisterPushDeviceRequest(string InstallationId, string ExpoPushToken, string Platform, string? AppVersion);

public sealed record UpdateNotificationPreferencesRequest(bool ChatEnabled, bool WorkoutRemindersEnabled, bool WorkoutUpdatesEnabled, bool ProgressEnabled, bool AccountEnabled, bool SoundEnabled, bool VibrationEnabled);
