using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class TrainerRewardsController : ApiControllerBase
{
    public TrainerRewardsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("configuration/trainer-rewards")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerRewardSettingsQuery(), cancellationToken)).ToActionResult();

    [HttpPut("configuration/trainer-rewards")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> UpdateSettings(UpdateTrainerRewardSettingsRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateTrainerRewardSettingsCommand(request.PointsEnabled, request.DefaultAwardPoints, request.RequireApprovalForAwards, request.AllowManualAdjustments), cancellationToken)).ToActionResult();

    [HttpGet("configuration/trainer-target-definitions")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Definitions(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerTargetDefinitionsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("configuration/trainer-target-definitions")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> CreateDefinition(CreateTrainerTargetDefinitionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateTrainerTargetDefinitionCommand(request.Name, request.Description, request.Metric, request.DefaultGoal, request.Period, request.DefaultAwardPoints, request.Status), cancellationToken)).ToActionResult();

    [HttpPut("configuration/trainer-target-definitions/{id:guid}")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> UpdateDefinition(Guid id, UpdateTrainerTargetDefinitionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateTrainerTargetDefinitionCommand(id, request.Name, request.Description, request.Metric, request.DefaultGoal, request.Period, request.DefaultAwardPoints), cancellationToken)).ToActionResult();

    [HttpPost("configuration/trainer-target-definitions/{id:guid}/status")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> DefinitionStatus(Guid id, SetTrainerTargetDefinitionStatusRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SetTrainerTargetDefinitionStatusCommand(id, request.Status), cancellationToken)).ToActionResult();

    [HttpGet("trainers/reward-assignments")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Assignments(Guid? trainerId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerTargetAssignmentsQuery(trainerId), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/reward-assignments")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Assign(Guid id, AssignTrainerTargetRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AssignTrainerTargetCommand(id, request.DefinitionId, request.GoalValue, request.EffectiveStart, request.EffectiveEnd), cancellationToken)).ToActionResult();

    [HttpPost("trainers/reward-assignments/{assignmentId:guid}/complete")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Complete(Guid assignmentId, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompleteTrainerTargetAssignmentCommand(assignmentId), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/targets")]
    public async Task<IActionResult> MyTargets(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMyTrainerTargetsQuery(), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/targets/{assignmentId:guid}/submit")]
    public async Task<IActionResult> SubmitMyTarget(Guid assignmentId, SubmitMyTrainerTargetRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SubmitMyTrainerTargetCommand(assignmentId, request.Details), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/reward-balance")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Balance(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerRewardBalanceQuery(id), cancellationToken)).ToActionResult();

    [HttpGet("trainers/{id:guid}/reward-transactions")]
    [RequiresPermission(PermissionCodes.TrainerRead)]
    public async Task<IActionResult> Transactions(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListTrainerRewardTransactionsQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("trainers/reward-assignments/{assignmentId:guid}/award")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Award(Guid assignmentId, AwardTrainerRewardRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AwardTrainerRewardCommand(assignmentId, request.Points, request.IdempotencyKey, request.Reason), cancellationToken)).ToActionResult();

    [HttpPost("trainers/{id:guid}/reward-adjustments")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Adjust(Guid id, AdjustTrainerRewardRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AdjustTrainerRewardCommand(id, request.Points, request.IdempotencyKey, request.Reason), cancellationToken)).ToActionResult();

    [HttpPost("trainers/reward-transactions/{transactionId:guid}/reverse")]
    [RequiresPermission(PermissionCodes.TrainerUpdate)]
    public async Task<IActionResult> Reverse(Guid transactionId, ReverseTrainerRewardRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ReverseTrainerRewardCommand(transactionId, request.IdempotencyKey, request.Reason), cancellationToken)).ToActionResult();
}

public sealed record UpdateTrainerRewardSettingsRequest(bool PointsEnabled, int DefaultAwardPoints, bool RequireApprovalForAwards, bool AllowManualAdjustments);

public sealed record CreateTrainerTargetDefinitionRequest(string Name, string? Description, TrainerTargetType Metric, decimal DefaultGoal, TrainerTargetPeriod Period, int DefaultAwardPoints, string? Status);

public sealed record UpdateTrainerTargetDefinitionRequest(string Name, string? Description, TrainerTargetType Metric, decimal DefaultGoal, TrainerTargetPeriod Period, int DefaultAwardPoints);

public sealed record SetTrainerTargetDefinitionStatusRequest(TrainerTargetDefinitionStatus Status);

public sealed record AssignTrainerTargetRequest(Guid DefinitionId, decimal? GoalValue, DateOnly EffectiveStart, DateOnly? EffectiveEnd);

public sealed record SubmitMyTrainerTargetRequest(string Details);

public sealed record AwardTrainerRewardRequest(int? Points, string IdempotencyKey, string? Reason);

public sealed record AdjustTrainerRewardRequest(int Points, string IdempotencyKey, string? Reason);

public sealed record ReverseTrainerRewardRequest(string IdempotencyKey, string? Reason);
