using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Staffing;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
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
    public async Task<IActionResult> List(int? page, int? pageSize, string? search, string? status, string? specialization, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainersQuery(page ?? 1, pageSize ?? 20, search, status, specialization), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("trainers")]
    [RequiresPermission(PermissionCodes.StaffCreate)]
    public async Task<IActionResult> Add(AddCoachRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddCoachCommand(request.Email, request.FirstName, request.LastName, request.Phone, request.Specialization, request.BranchId, request.SendActivationLink), cancellationToken)).ToActionResult();

    [HttpDelete("trainers/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteTrainerProfileCommand(id), cancellationToken)).ToActionResult();

    [HttpPut("trainers/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Update(Guid id, UpdateTrainerRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateTrainerCommand(id, request.Bio, request.Specialization, request.ExperienceYears, request.Certification, request.ProfileImageUrl, request.BranchId, request.Status, request.FirstName, request.LastName, request.Phone, request.SetPhone, request.EmployeeCode, request.JoiningDate, request.CommissionEligible, request.SetEmployment), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetTrainerPhotoQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpPost("trainers/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    [RequestSizeLimit(2_097_152)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 2_097_152)
        {
            return Result.Failure(GymErrors.InvalidPhoto).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (await Sender.Send(new UploadTrainerPhotoCommand(id, file.ContentType, buffer.ToArray()), cancellationToken)).ToActionResult();
    }

    [HttpDelete("trainers/{id:guid}/profile-photo")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> RemovePhoto(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new RemoveTrainerPhotoCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("trainers/workspace")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Workspace(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerWorkspaceQuery(), cancellationToken)).ToActionResult();

    [HttpGet("trainers/candidates")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Candidates(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerCandidatesQuery(), cancellationToken)).ToActionResult();

    [HttpGet("trainers/performance")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Performance(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerPerformanceQuery(from, to), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/workspace")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Details(Guid id, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerWorkspaceQuery(id, from, to), cancellationToken)).ToActionResult();

    [HttpPost("trainers/from-user")]
    [RequiresPermission(PermissionCodes.StaffCreate)]
    public async Task<IActionResult> FromUser(CreateTrainerProfileRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTrainerProfileCommand(request.UserId, request.EmployeeCode, request.Specialization, request.Bio, request.JoiningDate, request.CommissionEligible), cancellationToken)).ToActionResult();

    [HttpPut("trainers/{id:guid}/employment")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Employment(Guid id, UpdateTrainerEmploymentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateTrainerEmploymentCommand(id, request.EmployeeCode, request.Specialization, request.Bio, request.JoiningDate, request.CommissionEligible, request.Status), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/members")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Assign(Guid id, AssignTrainerMemberRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignTrainerMemberCommand(id, request.MemberId, request.StartDate), cancellationToken)).ToActionResult();

    [HttpPost("trainers/assignments/{assignmentId:guid}/end")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> EndAssignment(Guid assignmentId, EndTrainerAssignmentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new EndTrainerAssignmentCommand(assignmentId, request.EndDate), cancellationToken)).ToActionResult();

    [HttpPost("trainers/assignments/{assignmentId:guid}/change")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> ChangeAssignment(Guid assignmentId, ChangeTrainerAssignmentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ChangeTrainerAssignmentCommand(assignmentId, request.TrainerId, request.StartDate), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/targets")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Targets(Guid id, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerTargetsQuery(id, from, to), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/targets")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> AddTarget(Guid id, CreateTrainerTargetRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTrainerTargetCommand(id, request.TargetType, request.Period, request.Start, request.End, request.TargetValue, request.Name), cancellationToken)).ToActionResult();

    [HttpPut("trainers/incentive-rules")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Incentive(SaveTrainerIncentiveRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveTrainerIncentiveRuleCommand(request.TargetType, request.Percent), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/coach")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Coach(Guid id, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetCoachWorkspaceQuery(id, from, to), cancellationToken)).ToActionResult();

    [HttpPost("trainers/targets/{targetId:guid}/active")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> TargetActive(Guid targetId, SetTrainerTargetActiveRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetTrainerTargetActiveCommand(targetId, request.Active), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/commission-rules")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Commission(Guid id, CreateCommissionRuleRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateCommissionRuleCommand(id, request.Name, request.Kind, request.Percent, request.FixedAmount, request.Start, request.End, request.RequiresApproval), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/performance-incentives")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> PerformanceIncentive(Guid id, CreatePerformanceIncentiveRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreatePerformanceIncentiveCommand(id, request.Name, request.Metric, request.Threshold, request.RewardKind, request.FixedAmount, request.Percent, request.Start, request.End, request.RequiresApproval), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/compensation/calculate")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Calculate(Guid id, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new CalculateCoachCompensationCommand(id, from, to), cancellationToken)).ToActionResult();

    [HttpPost("trainers/compensation/{calculationId:guid}/decide")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Decide(Guid calculationId, DecideCoachCompensationRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new DecideCoachCompensationCommand(calculationId, request.Status), cancellationToken)).ToActionResult();
}

public sealed record CreateTrainerProfileRequest(Guid UserId, string? EmployeeCode, string? Specialization, string? Bio, DateOnly? JoiningDate, bool CommissionEligible);

public sealed record UpdateTrainerEmploymentRequest(string? EmployeeCode, string? Specialization, string? Bio, DateOnly? JoiningDate, bool CommissionEligible, ProfileStatus Status);

public sealed record AssignTrainerMemberRequest(Guid MemberId, DateOnly StartDate);

public sealed record EndTrainerAssignmentRequest(DateOnly EndDate);

public sealed record ChangeTrainerAssignmentRequest(Guid TrainerId, DateOnly StartDate);

public sealed record CreateTrainerTargetRequest(TrainerTargetType TargetType, TrainerTargetPeriod Period, DateOnly Start, DateOnly End, decimal TargetValue, string? Name = null);

public sealed record SaveTrainerIncentiveRequest(TrainerTargetType TargetType, decimal Percent);

public sealed record SetTrainerTargetActiveRequest(bool Active);

public sealed record CreateCommissionRuleRequest(string Name, CommissionRuleKind Kind, decimal? Percent, decimal? FixedAmount, DateOnly Start, DateOnly? End, bool RequiresApproval);

public sealed record CreatePerformanceIncentiveRequest(string Name, TrainerTargetType Metric, decimal Threshold, IncentiveRewardKind RewardKind, decimal? FixedAmount, decimal? Percent, DateOnly Start, DateOnly? End, bool RequiresApproval);

public sealed record DecideCoachCompensationRequest(CompensationStatus Status);
