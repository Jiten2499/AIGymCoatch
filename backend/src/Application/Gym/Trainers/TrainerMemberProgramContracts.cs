using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed record ListMemberProgramsQuery(Guid MemberId, int Page = 1, int PageSize = 20) : IRequest<Result<PageResult<ProgramResponse>>>;

public sealed record GetMemberProgramQuery(Guid MemberId, Guid ProgramId) : IRequest<Result<ProgramResponse>>;

public sealed record UpdateMemberProgramCommand(
    Guid MemberId,
    Guid ProgramId,
    string Name,
    string? Description,
    FitnessGoalType GoalType,
    WorkoutDifficulty Difficulty,
    int DurationWeeks,
    int FrequencyPerWeek,
    IReadOnlyList<ProgramDayInput> Days) : IRequest<Result<ProgramResponse>>;

public sealed record CreateMemberProgramCommand(
    Guid MemberId,
    string Name,
    string? Description,
    FitnessGoalType GoalType,
    WorkoutDifficulty Difficulty,
    int DurationWeeks,
    int FrequencyPerWeek,
    IReadOnlyList<ProgramDayInput> Days) : IRequest<Result<ProgramResponse>>;

public sealed record DeleteMemberProgramCommand(Guid MemberId, Guid ProgramId) : IRequest<Result<Guid>>;
