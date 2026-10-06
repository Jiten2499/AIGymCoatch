using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Staffing;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class TrainersController : ApiControllerBase
{
    public TrainersController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("trainers")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainersQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("trainers")]
    [RequiresPermission(PermissionCodes.StaffCreate)]
    public async Task<IActionResult> Add(AddCoachRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddCoachCommand(request.Email, request.FirstName, request.LastName, request.Phone, request.Specialization, request.BranchId), cancellationToken)).ToActionResult();

    [HttpPut("trainers/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateTrainerRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateTrainerCommand(id, request.Bio, request.Specialization, request.ExperienceYears, request.Certification, request.ProfileImageUrl, request.BranchId, request.Status, request.FirstName, request.LastName), cancellationToken)).ToActionResult();
}
