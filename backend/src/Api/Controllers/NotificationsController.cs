using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Challenges;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("notifications/{id:guid}/read")]
    [RequiresPermission(PermissionCodes.NotificationRead)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new MarkNotificationReadCommand(id), cancellationToken)).ToActionResult();
}
