using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Challenges;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ChallengesController : ApiControllerBase
{
    public ChallengesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("challenges")]
    [RequiresPermission(PermissionCodes.ChallengeRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListChallengesQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("challenges")]
    [RequiresPermission(PermissionCodes.ChallengeManage)]
    public async Task<IActionResult> Create(CreateChallengeRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateChallengeCommand(request.Name, request.Description, request.ChallengeType, request.StartDate, request.EndDate, request.TargetValue), cancellationToken)).ToActionResult();

    [HttpPost("challenges/{id:guid}/join")]
    [RequiresPermission(PermissionCodes.ChallengeRead)]
    public async Task<IActionResult> Join(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new JoinChallengeCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("challenges/{id:guid}/progress")]
    [RequiresPermission(PermissionCodes.ChallengeRead)]
    public async Task<IActionResult> Progress(Guid id, RecordChallengeProgressRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordChallengeProgressCommand(id, request.Value), cancellationToken)).ToActionResult();
}
