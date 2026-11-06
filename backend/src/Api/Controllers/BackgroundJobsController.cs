using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Platform.BackgroundJobs;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class BackgroundJobsController : ApiControllerBase
{
    public BackgroundJobsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("background-jobs")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> List(string? term, string? jobType, BackgroundJobStatus? status, string? queue, Guid? tenantId, DateTime? from, DateTime? to, string? correlationId, string? sourceType, int? page, int? pageSize, string? sort, bool? descending, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListBackgroundJobsQuery(term, jobType, status, queue, tenantId, from, to, correlationId, sourceType, page ?? 1, pageSize ?? 20, sort, descending ?? true), cancellationToken)).ToActionResult();

    [HttpGet("background-jobs/summary")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> Summary(Guid? tenantId, CancellationToken cancellationToken) =>
        (await Sender.Send(new BackgroundJobSummaryQuery(tenantId), cancellationToken)).ToActionResult();

    [HttpGet("background-jobs/queues")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> Queues(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListBackgroundJobQueuesQuery(), cancellationToken)).ToActionResult();

    [HttpGet("background-jobs/{jobId}")]
    [RequiresPermission(PermissionCodes.BackgroundJobRead)]
    public async Task<IActionResult> Get(string jobId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetBackgroundJobQuery(jobId), cancellationToken)).ToActionResult();

    [HttpPost("background-jobs/{jobId}/retry")]
    [RequiresPermission(PermissionCodes.BackgroundJobManage)]
    public async Task<IActionResult> Retry(string jobId, CancellationToken cancellationToken) =>
        (await Sender.Send(new RetryBackgroundJobCommand(jobId), cancellationToken)).ToActionResult();

    [HttpPost("background-jobs/{jobId}/cancel")]
    [RequiresPermission(PermissionCodes.BackgroundJobManage)]
    public async Task<IActionResult> Cancel(string jobId, CancellationToken cancellationToken) =>
        (await Sender.Send(new CancelBackgroundJobCommand(jobId), cancellationToken)).ToActionResult();
}
