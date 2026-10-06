using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Fitness;

public static class FitnessErrors
{
    public static readonly Error NotFound = new("fitness.not_found", "The requested fitness record was not found.");
    public static readonly Error InvalidState = new("fitness.invalid_state", "The fitness record cannot change to that state.");
    public static readonly Error InvalidReference = new("fitness.invalid_state", "Store an object-storage key, not a web address.");
}

public static class AiErrors
{
    public static readonly Error Unavailable = new("ai.unavailable", "The AI service is not available.");
    public static readonly Error Quota = new("ai.quota", "The AI usage limit for this workspace has been reached.");
    public static readonly Error Safety = new("ai.safety", AiSafetyPolicy.Refusal);
    public static readonly Error InvalidOutput = new("ai.invalid_output", "The AI response could not be applied.");
}

public interface IAiLimits
{
    string Provider { get; }

    string Model { get; }

    int MaxTokens { get; }

    int MaxPromptCharacters { get; }

    int DailyRequestLimit { get; }

    int MonthlyRequestLimit { get; }

    int TenantDailyRequestLimit { get; }

    int TenantMonthlyRequestLimit { get; }

    decimal CostPerThousandTokens { get; }
}

public interface IAiProvider
{
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken);
}

public sealed record AiCompletionRequest(string Model, IReadOnlyList<AiPromptMessage> Messages, int MaxTokens);

public sealed record AiPromptMessage(string Role, string Content);

public sealed record AiCompletionResult(bool Succeeded, string? Content, int InputTokens, int OutputTokens, string FailureCode, string? Provider = null, string? Model = null);

public interface IAiSafetyService
{
    Result Screen(string? text);
}

public interface IAiQuotaService
{
    Task<Result> AllowAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}

public interface IAiContextBuilder
{
    Task<AiUserContext> BuildAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}

public sealed record AiUserContext(string Goal, string FitnessLevel, string ActivityLevel, string UnitSystem, int PreferredDays, int PreferredMinutes, IReadOnlyList<string> RecentWorkoutDates, decimal Calories, decimal CalorieTarget, decimal Protein, decimal ProteinTarget, string? ActiveChallenge);

public interface IFitnessRepository
{
    Task<FitnessProfile?> GetProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddProfileAsync(FitnessProfile profile, CancellationToken cancellationToken);

    Task<IReadOnlyList<FitnessGoal>> ListGoalsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<FitnessGoal?> GetGoalAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddGoalAsync(FitnessGoal goal, CancellationToken cancellationToken);

    Task<PageResult<Exercise>> ListExercisesAsync(Guid tenantId, string? search, PageQuery page, CancellationToken cancellationToken);

    Task<Exercise?> GetExerciseAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddExerciseAsync(Exercise exercise, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExerciseOption>> ListExerciseOptionsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PageResult<BodyMeasurement>> ListMeasurementsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken);

    Task AddMeasurementAsync(BodyMeasurement measurement, CancellationToken cancellationToken);

    Task AddScanAsync(BodyScanRecord scan, CancellationToken cancellationToken);
}

public interface IWorkoutRepository
{
    Task<PageResult<ProgramResponse>> ListProgramsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken);

    Task<ProgramResponse?> GetProgramAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task<bool> ProgramVisibleAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddProgramAsync(WorkoutProgram program, CancellationToken cancellationToken);

    Task AddDayAsync(WorkoutProgramDay day, CancellationToken cancellationToken);

    Task AddProgramExerciseAsync(WorkoutProgramExercise exercise, CancellationToken cancellationToken);

    Task<PageResult<SessionResponse>> ListSessionsAsync(Guid tenantId, Guid userId, SessionStatus? status, PageQuery page, CancellationToken cancellationToken);

    Task<SessionResponse?> GetSessionAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task<WorkoutSession?> GetSessionEntityAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddSessionAsync(WorkoutSession session, CancellationToken cancellationToken);

    Task<int> NextSessionSequenceAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddSessionExerciseAsync(WorkoutSessionExercise exercise, CancellationToken cancellationToken);

    Task<WorkoutSessionExercise?> GetOwnedSessionExerciseAsync(Guid tenantId, Guid userId, Guid sessionExerciseId, CancellationToken cancellationToken);

    Task<int> NextSetNumberAsync(Guid sessionExerciseId, CancellationToken cancellationToken);

    Task AddSetAsync(WorkoutSet set, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalRecordResponse>> ListRecordsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> RecentWorkoutLabelsAsync(Guid tenantId, Guid userId, int take, CancellationToken cancellationToken);

    Task<int> CompletedCountAsync(Guid tenantId, Guid userId, DateTime fromUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateOnly>> RecentCompletedDatesAsync(Guid tenantId, Guid userId, DateOnly from, CancellationToken cancellationToken);
}

public interface INutritionRepository
{
    Task<NutritionProfile?> GetProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task AddProfileAsync(NutritionProfile profile, CancellationToken cancellationToken);

    Task<PageResult<NutritionLog>> ListLogsAsync(Guid tenantId, Guid userId, DateOnly? date, PageQuery page, CancellationToken cancellationToken);

    Task AddLogAsync(NutritionLog log, CancellationToken cancellationToken);

    Task<NutritionSummaryResponse> SummarizeDayAsync(Guid tenantId, Guid userId, DateOnly date, CancellationToken cancellationToken);
}

public interface IAiRepository
{
    Task<AiConversation?> GetConversationAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task<PageResult<AiConversation>> ListConversationsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken);

    Task AddConversationAsync(AiConversation conversation, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiMessage>> ListRecentMessagesAsync(Guid conversationId, int take, CancellationToken cancellationToken);

    Task<PageResult<AiMessage>> ListMessagesAsync(Guid conversationId, PageQuery page, CancellationToken cancellationToken);

    Task AddMessageAsync(AiMessage message, CancellationToken cancellationToken);

    Task AddUsageAsync(AiUsageRecord usage, CancellationToken cancellationToken);

    Task<int> CountUsageAsync(Guid tenantId, Guid userId, DateTime fromUtc, CancellationToken cancellationToken);

    Task<int> CountTenantUsageAsync(Guid tenantId, DateTime fromUtc, CancellationToken cancellationToken);
}

public interface IChallengeRepository
{
    Task<PageResult<ChallengeResponse>> ListAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken);

    Task<Challenge?> GetVisibleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Challenge challenge, CancellationToken cancellationToken);

    Task<ChallengeParticipant?> GetParticipantAsync(Guid challengeId, Guid userId, CancellationToken cancellationToken);

    Task AddParticipantAsync(ChallengeParticipant participant, CancellationToken cancellationToken);

    Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken);

    Task<PageResult<Notification>> ListNotificationsAsync(Guid tenantId, Guid userId, PageQuery page, CancellationToken cancellationToken);

    Task<Notification?> GetNotificationAsync(Guid tenantId, Guid userId, Guid id, CancellationToken cancellationToken);

    Task RecordWorkoutProgressAsync(Guid tenantId, Guid userId, bool firstWorkoutToday, DateTime utcNow, CancellationToken cancellationToken);

    Task<ChallengeResponse?> ActiveChallengeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public interface IMemberDashboardQuery
{
    Task<DashboardResponse> GetAsync(Guid tenantId, Guid userId, DateOnly today, CancellationToken cancellationToken);
}

internal static class Scope
{
    public static async Task<Result<(Guid UserId, Guid TenantId)>> Open(IOperationGuard guard, ICurrentActor actor, string permission, string feature, CancellationToken cancellationToken)
    {
        var gate = await guard.AllowAsync(permission, feature, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || actor.UserId is not Guid userId || actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<(Guid, Guid)>(gate.IsFailure ? gate.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((userId, tenantId));
    }
}
