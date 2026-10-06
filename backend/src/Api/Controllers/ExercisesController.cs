using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Exercises;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class ExercisesController : ApiControllerBase
{
    public ExercisesController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("exercises")]
    [RequiresPermission(PermissionCodes.ExerciseRead)]
    public async Task<IActionResult> List(string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListExercisesQuery(search, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("exercises/{id:guid}")]
    [RequiresPermission(PermissionCodes.ExerciseRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetExerciseQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("exercises")]
    [RequiresPermission(PermissionCodes.ExerciseManage)]
    public async Task<IActionResult> Create(CreateExerciseRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateExerciseCommand(request.Name, request.Description, request.Instructions, request.MuscleGroup, request.Equipment, request.ExerciseType, request.Difficulty), cancellationToken)).ToActionResult();
}
