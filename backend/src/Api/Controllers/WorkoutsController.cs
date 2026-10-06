using LifeFitAI.Api.Contracts;
using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Progress;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class WorkoutsController : ApiControllerBase
{
    public WorkoutsController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("workouts")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> List(int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListProgramsQuery(page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("workouts")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> Create(CreateProgramRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateProgramCommand(request.Name, request.Description, request.GoalType, request.Difficulty, request.DurationWeeks, request.FrequencyPerWeek, Days(request)), cancellationToken)).ToActionResult();

    [HttpGet("workouts/{id:guid}")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetProgramQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("workouts/{id:guid}/start")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> Start(Guid id, Guid? dayId, CancellationToken cancellationToken) =>
        (await Sender.Send(new StartProgramCommand(id, dayId), cancellationToken)).ToActionResult();

    [HttpGet("workout-sessions")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> Sessions(SessionStatus? status, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListSessionsQuery(status, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateSessionCommand(request.ProgramId, request.DayId, request.Notes), cancellationToken)).ToActionResult();

    [HttpGet("workout-sessions/{id:guid}")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetSessionQuery(id), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions/{id:guid}/start")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> StartSession(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new StartSessionCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions/{id:guid}/complete")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> CompleteSession(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompleteSessionCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions/{id:guid}/cancel")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken) =>
        (await Sender.Send(new CancelSessionCommand(id), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions/{id:guid}/exercises")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> AddExercise(Guid id, AddSessionExerciseRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddSessionExerciseCommand(id, request.ExerciseId, request.Notes), cancellationToken)).ToActionResult();

    [HttpPost("workout-sessions/exercises/{id:guid}/sets")]
    [RequiresPermission(PermissionCodes.WorkoutManage)]
    public async Task<IActionResult> AddSet(Guid id, AddSetRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddSetCommand(id, request.Reps, request.Weight, request.DurationSeconds, request.Distance, request.Rpe, request.Completed), cancellationToken)).ToActionResult();

    [HttpGet("progress/records")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> Records(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListRecordsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("progress")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> Progress(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetProgressQuery(), cancellationToken)).ToActionResult();

    [HttpGet("progress/summary")]
    [RequiresPermission(PermissionCodes.WorkoutRead)]
    public async Task<IActionResult> ProgressSummary(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetProgressQuery(), cancellationToken)).ToActionResult();

    private static ProgramDayInput[] Days(CreateProgramRequest request) =>
        (request.Days ?? []).Select(day => new ProgramDayInput(
            day.DayNumber,
            day.Name,
            day.Description,
            day.RestDay,
            (day.Exercises ?? []).Select(item => new ProgramExerciseInput(item.ExerciseId, item.Sets, item.TargetReps, item.TargetDurationSeconds, item.TargetDistance, item.TargetWeight, item.RestSeconds, item.Notes)).ToArray())).ToArray();
}
