using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Profile;
using LifeFitAI.Application.Fitness.Progress;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class FitnessController : ApiControllerBase
{
    public FitnessController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("fitness/profile")]
    [RequiresPermission(PermissionCodes.FitnessRead)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFitnessProfileQuery(), cancellationToken)).ToActionResult();

    [HttpPut("fitness/profile")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> SaveProfile(SaveFitnessProfileRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveFitnessProfileCommand(request.UnitSystem, request.Height, request.Weight, request.FitnessLevel, request.PrimaryGoal, request.ActivityLevel, request.TrainingExperience, request.PreferredWorkoutDays, request.PreferredWorkoutDuration, request.PreferredTrainingLocation), cancellationToken)).ToActionResult();

    [HttpGet("fitness/goals")]
    [RequiresPermission(PermissionCodes.FitnessRead)]
    public async Task<IActionResult> Goals(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListGoalsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("fitness/goals")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> CreateGoal(CreateGoalRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateGoalCommand(request.GoalType, request.TargetValue, request.CurrentValue, request.StartDate, request.TargetDate), cancellationToken)).ToActionResult();

    [HttpPut("fitness/goals/{id:guid}")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> UpdateGoal(Guid id, UpdateGoalRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateGoalCommand(id, request.TargetValue, request.CurrentValue, request.TargetDate), cancellationToken)).ToActionResult();

    [HttpPost("fitness/goals/{id:guid}/complete")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> CompleteGoal(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompleteGoalCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("fitness/goals/{id:guid}/cancel")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> CancelGoal(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new CancelGoalCommand(id), cancellationToken)).ToActionResult();

    [HttpGet("measurements")]
    [RequiresPermission(PermissionCodes.FitnessRead)]
    public async Task<IActionResult> Measurements(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMeasurementsQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("measurements")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> RecordMeasurement(RecordMeasurementRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordMeasurementCommand(request.UnitSystem, request.MeasurementDate, request.Weight, request.BodyFatPercentage, request.Chest, request.Waist, request.Hip, request.Arm, request.Thigh, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("measurements/scans")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> RecordScan(RecordBodyScanRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RecordBodyScanCommand(request.ScanDate, request.Source, request.StorageReference), cancellationToken)).ToActionResult();

    [HttpGet("member/dashboard")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberDashboardQuery(), cancellationToken)).ToActionResult();
}
