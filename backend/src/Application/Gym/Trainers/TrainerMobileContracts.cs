using LifeFitAI.Domain.Gym;
using MediatR;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record TrainerSessionItem(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string Status,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    string? SessionType,
    string? Location,
    string? Focus,
    string? Notes,
    int ElapsedSeconds,
    DateTime? StartedAtUtc,
    string? PackageName,
    int? SessionsRemaining,
    int? SessionCount);

public sealed record TrainerExerciseItem(Guid Id, string Name, string? MuscleGroup, int Sets, string Reps, bool Completed, int SortOrder);

public sealed record TrainerSessionDetailItem(TrainerSessionItem Session, IReadOnlyList<TrainerExerciseItem> Exercises);

public sealed record TrainerHomeItem(
    string FirstName,
    int SessionsToday,
    int ActiveMembers,
    int PendingNotes,
    int NewAssignments,
    TrainerSessionItem? NextSession,
    IReadOnlyList<TrainerSessionItem> Today,
    bool HasPhoto = false);

public sealed record TrainerMemberItem(
    Guid Id,
    string Name,
    string? Goal,
    string Status,
    string? PackageName,
    int? SessionsUsed,
    int? SessionCount,
    DateOnly? PackageEnd,
    bool Expiring,
    bool HasPhoto = false);

public sealed record TrainerPackageItem(string Name, string Status, int SessionsRemaining, int SessionCount, DateOnly EndDate, decimal Price, string Currency, bool Expiring);

public sealed record TrainerNoteItem(Guid SessionId, DateTime AtUtc, string Text);

public sealed record TrainerMeasurementItem(DateOnly Date, decimal? Weight, decimal? BodyFatPercentage, decimal? Waist);

public sealed record TrainerMemberDetailItem(
    Guid Id,
    string Name,
    string? Phone,
    string? Goal,
    string Status,
    DateOnly JoinDate,
    IReadOnlyList<TrainerPackageItem> Packages,
    IReadOnlyList<TrainerSessionItem> Sessions,
    IReadOnlyList<TrainerNoteItem> Notes,
    IReadOnlyList<TrainerMeasurementItem> Measurements,
    bool HasPhoto = false);

public sealed record TrainerMetricItem(Guid Id, string Name, string Metric, decimal Goal, decimal Actual, int Percent, string Status);

public sealed record TrainerCreditItem(Guid Id, int Points, string Type, string Status, string? Reason, DateTime CreatedAt);

public sealed record MyTrainerPerformanceItem(
    int CreditBalance,
    int OverallPercent,
    int SessionsCompleted,
    decimal PackageValue,
    string? Currency,
    int PackagesSold,
    int NewClients,
    IReadOnlyList<TrainerMetricItem> Metrics,
    IReadOnlyList<TrainerCreditItem> Credits);

public sealed record TrainerAlertItem(string Kind, string Title, string Body, DateTime AtUtc, Guid? SessionId, Guid? MemberId);

public sealed record MyTrainerProfileItem(
    string FirstName,
    string LastName,
    string Email,
    string? Specialization,
    string? Certification,
    string? Bio,
    int? ExperienceYears,
    DateOnly? JoiningDate,
    bool HasPhoto = false);

public sealed record GetTrainerHomeQuery : IRequest<Result<TrainerHomeItem>>;

public sealed record GetMyTrainerScheduleQuery(DateOnly From, DateOnly To) : IRequest<Result<IReadOnlyList<TrainerSessionItem>>>;

public sealed record GetMyTrainerSessionQuery(Guid SessionId) : IRequest<Result<TrainerSessionDetailItem>>;

public sealed record ListMyTrainerMembersQuery(string? Search) : IRequest<Result<IReadOnlyList<TrainerMemberItem>>>;

public sealed record GetMyTrainerMemberQuery(Guid MemberId) : IRequest<Result<TrainerMemberDetailItem>>;

public sealed record GetMyTrainerPerformanceQuery(DateOnly From, DateOnly To) : IRequest<Result<MyTrainerPerformanceItem>>;

public sealed record ListMyTrainerAlertsQuery : IRequest<Result<IReadOnlyList<TrainerAlertItem>>>;

public sealed record GetMyTrainerProfileQuery : IRequest<Result<MyTrainerProfileItem>>;

public sealed record ScheduleMyTrainerSessionCommand(Guid MemberId, DateTime StartUtc, DateTime EndUtc, string? SessionType, string? Location, string? Focus) : IRequest<Result<Guid>>;

public sealed record RescheduleMyTrainerSessionCommand(Guid SessionId, DateTime StartUtc, DateTime EndUtc) : IRequest<Result<Guid>>;

public sealed record StartMyTrainerSessionCommand(Guid SessionId) : IRequest<Result<Guid>>;

public sealed record RememberMyTrainerSessionCommand(Guid SessionId, int ElapsedSeconds) : IRequest<Result<Guid>>;

public sealed record AddMyTrainerSessionExerciseCommand(Guid SessionId, string Name, string? MuscleGroup, int Sets, string Reps) : IRequest<Result<Guid>>;

public sealed record ToggleMyTrainerSessionExerciseCommand(Guid SessionId, Guid ExerciseId) : IRequest<Result<Guid>>;

public sealed record CompleteMyTrainerSessionCommand(Guid SessionId, string? Notes, int ElapsedSeconds) : IRequest<Result<Guid>>;

public sealed record CancelMyTrainerSessionCommand(Guid SessionId) : IRequest<Result<Guid>>;

public interface ITrainerMobileStore
{
    Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<(string FirstName, string LastName, string Email)?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsAssignedAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly day, CancellationToken cancellationToken);

    Task<IReadOnlyList<PtSession>> ListSessionsAsync(Guid tenantId, Guid trainerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    Task<PtSession?> GetSessionAsync(Guid tenantId, Guid trainerId, Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PtSessionExercise>> ListExercisesAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberProfile>> ListAssignedMembersAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<MemberProfile?> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberPtAssignment>> ListMemberPackagesAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberPtAssignment>> OpenPackagesAsync(Guid tenantId, Guid trainerId, Guid memberId, DateOnly day, CancellationToken cancellationToken);

    Task<IReadOnlyList<GymMembership>> OpenMembershipsAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerMemberAssignment>> RecentAssignmentsAsync(Guid tenantId, Guid trainerId, DateOnly since, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerTargetAssignment>> ListTargetsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainerRewardTransaction>> ListRewardsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateOnly>> CompletedDatesAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberPtAssignment>> SoldPackagesAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateOnly>> ClientStartsAsync(Guid tenantId, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Fitness.BodyMeasurement>> ListMeasurementsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> MemberNamesAsync(Guid tenantId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken);

    Task AddSessionAsync(PtSession session, CancellationToken cancellationToken);

    Task AddExerciseAsync(PtSessionExercise exercise, CancellationToken cancellationToken);

    Task<PtSessionExercise?> GetExerciseAsync(Guid tenantId, Guid sessionId, Guid exerciseId, CancellationToken cancellationToken);

    Task<int> NextExerciseOrderAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken);

    Task AddConsumptionAsync(PtSessionConsumption consumption, CancellationToken cancellationToken);

    Task<bool> SessionConsumedAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Fitness.MemberProgressAssessment>> ListProgressAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken);

    Task<Domain.Fitness.MemberProgressAssessment?> GetProgressAsync(Guid tenantId, Guid memberId, Guid assessmentId, CancellationToken cancellationToken);

    Task AddProgressAsync(Domain.Fitness.MemberProgressAssessment assessment, CancellationToken cancellationToken);

    Task<Domain.Fitness.BodyMeasurement?> GetMeasurementAsync(Guid tenantId, Guid measurementId, CancellationToken cancellationToken);

    Task AddMeasurementAsync(Domain.Fitness.BodyMeasurement measurement, CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Fitness.MemberProgressPhoto>> ListProgressPhotosAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken);

    Task<List<Domain.Fitness.MemberProgressPhoto>> ListAssessmentPhotosAsync(Guid tenantId, Guid assessmentId, CancellationToken cancellationToken);

    Task AddProgressPhotoAsync(Domain.Fitness.MemberProgressPhoto photo, CancellationToken cancellationToken);
}
