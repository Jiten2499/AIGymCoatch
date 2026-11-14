using LifeFitAI.Api.Endpoints;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Trainers;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeFitAI.Api.Controllers;

[Authorize]
public sealed class TrainerMobileController : ApiControllerBase
{
    public TrainerMobileController(MediatR.ISender sender) : base(sender)
    {
    }

    [HttpGet("trainers/me/home")]
    public async Task<IActionResult> Home(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetTrainerHomeQuery(), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/schedule")]
    public async Task<IActionResult> Schedule(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTrainerScheduleQuery(from, to), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/sessions/{sessionId:guid}")]
    public async Task<IActionResult> Session(Guid sessionId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTrainerSessionQuery(sessionId), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members")]
    public async Task<IActionResult> Members(string? search, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMyTrainerMembersQuery(search), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members/{memberId:guid}")]
    public async Task<IActionResult> Member(Guid memberId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTrainerMemberQuery(memberId), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members/{memberId:guid}/profile-photo")]
    public async Task<IActionResult> MemberPhoto(Guid memberId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetMyTrainerMemberPhotoQuery(memberId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("trainers/me/members/{memberId:guid}/workouts")]
    public async Task<IActionResult> MemberWorkouts(Guid memberId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberProgramsQuery(memberId, page ?? 1, pageSize ?? 20), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/members/{memberId:guid}/workouts")]
    public async Task<IActionResult> CreateMemberWorkout(Guid memberId, UpdateMemberProgramRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CreateMemberProgramCommand(
            memberId,
            request.Name,
            request.Description,
            request.GoalType,
            request.Difficulty,
            request.DurationWeeks,
            request.FrequencyPerWeek,
            MemberProgramRequestMap.Days(request)), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members/{memberId:guid}/workouts/{programId:guid}")]
    public async Task<IActionResult> MemberWorkout(Guid memberId, Guid programId, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMemberProgramQuery(memberId, programId), cancellationToken)).ToActionResult();

    [HttpPut("trainers/me/members/{memberId:guid}/workouts/{programId:guid}")]
    public async Task<IActionResult> UpdateMemberWorkout(Guid memberId, Guid programId, UpdateMemberProgramRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new UpdateMemberProgramCommand(
            memberId,
            programId,
            request.Name,
            request.Description,
            request.GoalType,
            request.Difficulty,
            request.DurationWeeks,
            request.FrequencyPerWeek,
            MemberProgramRequestMap.Days(request)), cancellationToken)).ToActionResult();

    [HttpDelete("trainers/me/members/{memberId:guid}/workouts/{programId:guid}")]
    public async Task<IActionResult> DeleteMemberWorkout(Guid memberId, Guid programId, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteMemberProgramCommand(memberId, programId), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members/{memberId:guid}/progress")]
    public async Task<IActionResult> MemberProgress(Guid memberId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMemberProgressQuery(memberId), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/members/{memberId:guid}/progress")]
    public async Task<IActionResult> RecordProgress(Guid memberId, SaveMemberProgressRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(TrainerProgressRequests.ProgressCommand(memberId, null, request), cancellationToken)).ToActionResult();

    [HttpPut("trainers/me/members/{memberId:guid}/progress/{assessmentId:guid}")]
    public async Task<IActionResult> ReviseProgress(Guid memberId, Guid assessmentId, SaveMemberProgressRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(TrainerProgressRequests.ProgressCommand(memberId, assessmentId, request), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/members/{memberId:guid}/progress/{assessmentId:guid}/photo")]
    [RequestSizeLimit(2_097_152)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_097_152)]
    public async Task<IActionResult> ProgressPhoto(Guid memberId, Guid assessmentId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > 2_097_152)
        {
            return Result.Failure<Guid>(GymErrors.InvalidPhoto).ToActionResult();
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (await Sender.Send(new UploadMemberProgressPhotoCommand(memberId, assessmentId, file.ContentType, buffer.ToArray()), cancellationToken)).ToActionResult();
    }

    [HttpGet("trainers/me/members/{memberId:guid}/progress/{assessmentId:guid}/photos/{photoId:guid}")]
    public Task<IActionResult> ProgressPhotoFile(Guid memberId, Guid assessmentId, Guid photoId, CancellationToken cancellationToken) =>
        ProgressPhoto(memberId, assessmentId, cancellationToken, photoId);

    [HttpDelete("trainers/me/members/{memberId:guid}/progress/{assessmentId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> RemoveProgressPhoto(Guid memberId, Guid assessmentId, Guid photoId, CancellationToken cancellationToken) =>
        (await Sender.Send(new DeleteMemberProgressPhotoCommand(memberId, assessmentId, photoId), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/members/{memberId:guid}/progress/{assessmentId:guid}/photo")]
    public Task<IActionResult> ProgressPhoto(Guid memberId, Guid assessmentId, CancellationToken cancellationToken) =>
        ProgressPhoto(memberId, assessmentId, cancellationToken, null);

    private async Task<IActionResult> ProgressPhoto(Guid memberId, Guid assessmentId, CancellationToken cancellationToken, Guid? photoId)
    {
        var result = await Sender.Send(new ReadMemberProgressPhotoQuery(memberId, assessmentId, photoId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("trainers/me/performance")]
    public async Task<IActionResult> Performance(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTrainerPerformanceQuery(from, to), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/alerts")]
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken) =>
        (await Sender.Send(new ListMyTrainerAlertsQuery(), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        (await Sender.Send(new GetMyTrainerProfileQuery(), cancellationToken)).ToActionResult();

    [HttpGet("trainers/me/profile-photo")]
    public async Task<IActionResult> Photo(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetMyTrainerPhotoQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpPost("trainers/me/sessions")]
    public async Task<IActionResult> ScheduleSession(ScheduleTrainerSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new ScheduleMyTrainerSessionCommand(request.MemberId, request.StartUtc, request.EndUtc, request.SessionType, request.Location, request.Focus), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid sessionId, RescheduleTrainerSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RescheduleMyTrainerSessionCommand(sessionId, request.StartUtc, request.EndUtc), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/start")]
    public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken) =>
        (await Sender.Send(new StartMyTrainerSessionCommand(sessionId), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/progress")]
    public async Task<IActionResult> Progress(Guid sessionId, TrainerSessionProgressRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new RememberMyTrainerSessionCommand(sessionId, request.ElapsedSeconds), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/exercises")]
    public async Task<IActionResult> Exercise(Guid sessionId, TrainerExerciseRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new AddMyTrainerSessionExerciseCommand(sessionId, request.Name, request.MuscleGroup, request.Sets, request.Reps), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/exercises/{exerciseId:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid sessionId, Guid exerciseId, CancellationToken cancellationToken) =>
        (await Sender.Send(new ToggleMyTrainerSessionExerciseCommand(sessionId, exerciseId), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid sessionId, CompleteTrainerSessionRequest request, CancellationToken cancellationToken) =>
        (await Sender.Send(new CompleteMyTrainerSessionCommand(sessionId, request.Notes, request.ElapsedSeconds), cancellationToken)).ToActionResult();

    [HttpPost("trainers/me/sessions/{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken) =>
        (await Sender.Send(new CancelMyTrainerSessionCommand(sessionId), cancellationToken)).ToActionResult();
}

public sealed record UpdateMemberProgramRequest(
    string Name,
    string? Description,
    FitnessGoalType GoalType,
    WorkoutDifficulty Difficulty,
    int DurationWeeks,
    int FrequencyPerWeek,
    IReadOnlyList<UpdateMemberProgramDayRequest>? Days);

public sealed record UpdateMemberProgramDayRequest(
    int DayNumber,
    string Name,
    string? Description,
    bool RestDay,
    IReadOnlyList<UpdateMemberProgramExerciseRequest>? Exercises);

public sealed record UpdateMemberProgramExerciseRequest(
    Guid ExerciseId,
    int? Sets,
    int? TargetReps,
    int? TargetDurationSeconds,
    decimal? TargetDistance,
    decimal? TargetWeight,
    int? RestSeconds,
    string? Notes);

public sealed record ScheduleTrainerSessionRequest(Guid MemberId, DateTime StartUtc, DateTime EndUtc, string? SessionType, string? Location, string? Focus);

internal static class MemberProgramRequestMap
{
    public static ProgramDayInput[] Days(UpdateMemberProgramRequest request) =>
        (request.Days ?? []).Select(day => new ProgramDayInput(
            day.DayNumber,
            day.Name,
            day.Description,
            day.RestDay,
            (day.Exercises ?? []).Select(item => new ProgramExerciseInput(
                item.ExerciseId,
                item.Sets,
                item.TargetReps,
                item.TargetDurationSeconds,
                item.TargetDistance,
                item.TargetWeight,
                item.RestSeconds,
                item.Notes)).ToArray())).ToArray();
}

public sealed record RescheduleTrainerSessionRequest(DateTime StartUtc, DateTime EndUtc);

public sealed record TrainerSessionProgressRequest(int ElapsedSeconds);

public sealed record TrainerExerciseRequest(string Name, string? MuscleGroup, int Sets, string Reps);

public sealed record CompleteTrainerSessionRequest(string? Notes, int ElapsedSeconds);

public sealed record SaveMemberProgressRequest(
    DateOnly AssessmentDate,
    decimal? Weight,
    decimal? Waist,
    decimal? Chest,
    decimal? Hip,
    decimal? Arm,
    decimal? Thigh,
    decimal? BodyFatPercentage,
    decimal? FitnessScore,
    string? TrainingPerformance,
    string? Notes,
    string? AchievedGoals,
    string? NextGoals,
    bool PhotoConsent,
    decimal? Height = null,
    decimal? RightArm = null,
    decimal? RightThigh = null,
    decimal? VisceralFat = null,
    decimal? StrengthScore = null,
    decimal? EnduranceScore = null,
    decimal? FlexibilityScore = null,
    decimal? CoreScore = null);

internal static class TrainerProgressRequests
{
    public static SaveMemberProgressCommand ProgressCommand(Guid memberId, Guid? assessmentId, SaveMemberProgressRequest request) =>
        new(memberId, assessmentId, request.AssessmentDate, request.Weight, request.Waist, request.Chest, request.Hip, request.Arm, request.Thigh, request.BodyFatPercentage, request.FitnessScore, request.TrainingPerformance, request.Notes, request.AchievedGoals, request.NextGoals, request.PhotoConsent, request.Height, request.RightArm, request.RightThigh, request.VisceralFat, request.StrengthScore, request.EnduranceScore, request.FlexibilityScore, request.CoreScore);
}
