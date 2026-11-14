using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Assessment;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class FitnessAssessmentController : ApiControllerBase
{
    public FitnessAssessmentController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("fitness-assessment")]
    [RequiresPermission(PermissionCodes.FitnessRead)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFitnessAssessmentQuery(), cancellationToken)).ToActionResult();

    [HttpGet("fitness-assessment/completion")]
    [RequiresPermission(PermissionCodes.FitnessRead)]
    public async Task<IActionResult> Completion(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetFitnessAssessmentCompletionQuery(), cancellationToken)).ToActionResult();

    [HttpPost("fitness-assessment")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public Task<IActionResult> Create(SaveFitnessAssessmentRequest request, CancellationToken cancellationToken) =>
        Save(request, cancellationToken);

    [HttpPut("fitness-assessment")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public Task<IActionResult> Update(SaveFitnessAssessmentRequest request, CancellationToken cancellationToken) =>
        Save(request, cancellationToken);

    [HttpDelete("fitness-assessment")]
    [RequiresPermission(PermissionCodes.FitnessManage)]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteFitnessAssessmentCommand(), cancellationToken)).ToActionResult();

    private async Task<IActionResult> Save(SaveFitnessAssessmentRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new SaveFitnessAssessmentCommand(
            request.DateOfBirth,
            request.Gender,
            request.HeightCm,
            request.CurrentWeightKg,
            request.TargetWeightKg,
            request.WaistCircumferenceCm,
            request.BodyFatPercentage,
            request.Country,
            request.MeasurementUnit,
            request.PrimaryGoal,
            request.SecondaryGoal,
            request.GoalTimeline,
            request.CustomGoalTimelineDays,
            request.FitnessLevel,
            request.Priority,
            request.GoalDescription,
            request.Health,
            request.Workout,
            request.Diet,
            request.Lifestyle,
            request.Measurements,
            request.CurrentStep,
            request.Submit), cancellationToken)).ToActionResult();
}

public sealed record SaveFitnessAssessmentRequest(
    DateOnly? DateOfBirth,
    string? Gender,
    decimal? HeightCm,
    decimal? CurrentWeightKg,
    decimal? TargetWeightKg,
    decimal? WaistCircumferenceCm,
    decimal? BodyFatPercentage,
    string? Country,
    UnitSystem MeasurementUnit,
    string? PrimaryGoal,
    string? SecondaryGoal,
    string? GoalTimeline,
    int? CustomGoalTimelineDays,
    FitnessLevel? FitnessLevel,
    string? Priority,
    string? GoalDescription,
    AssessmentHealthSection? Health,
    AssessmentWorkoutSection? Workout,
    AssessmentDietSection? Diet,
    AssessmentLifestyleSection? Lifestyle,
    AssessmentMeasurementsSection? Measurements,
    int CurrentStep,
    bool Submit);
