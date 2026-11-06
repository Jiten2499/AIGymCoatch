using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class SystemJobsController : ApiControllerBase
{
    public SystemJobsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("system-jobs")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListSystemJobsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("system-jobs/{jobKey}")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> Get(string jobKey, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetSystemJobQuery(jobKey), cancellationToken)).ToActionResult();

    [HttpPost("system-jobs/{jobKey}/enable")]
    [RequiresPermission(PermissionCodes.BackgroundJobManage)]
    public async Task<IActionResult> Enable(string jobKey, CancellationToken cancellationToken) =>
        (await Sender.Send(new EnableSystemJobCommand(jobKey), cancellationToken)).ToActionResult();

    [HttpPost("system-jobs/{jobKey}/disable")]
    [RequiresPermission(PermissionCodes.BackgroundJobManage)]
    public async Task<IActionResult> Disable(string jobKey, CancellationToken cancellationToken) =>
        (await Sender.Send(new DisableSystemJobCommand(jobKey), cancellationToken)).ToActionResult();

    [HttpPost("system-jobs/{jobKey}/run")]
    [RequiresPermission(PermissionCodes.BackgroundJobManage)]
    public async Task<IActionResult> Run(string jobKey, CancellationToken cancellationToken) =>
        (await Sender.Send(new RunSystemJobCommand(jobKey), cancellationToken)).ToActionResult();
}
