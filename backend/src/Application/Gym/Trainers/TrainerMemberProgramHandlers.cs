using FluentValidation;
using LifeFitAI.Application.Fitness;
using LifeFitAI.Application.Fitness.Workout;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed class CreateMemberProgramCommandValidator : AbstractValidator<CreateMemberProgramCommand>
{
    public CreateMemberProgramCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DurationWeeks).InclusiveBetween(1, 52);
        RuleFor(command => command.FrequencyPerWeek).InclusiveBetween(1, 7);
        RuleFor(command => command.Days).NotEmpty();
        RuleForEach(command => command.Days).ChildRules(day =>
        {
            day.RuleFor(item => item.DayNumber).InclusiveBetween(1, 31);
            day.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class UpdateMemberProgramCommandValidator : AbstractValidator<UpdateMemberProgramCommand>
{
    public UpdateMemberProgramCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DurationWeeks).InclusiveBetween(1, 52);
        RuleFor(command => command.FrequencyPerWeek).InclusiveBetween(1, 7);
        RuleFor(command => command.Days).NotEmpty();
        RuleForEach(command => command.Days).ChildRules(day =>
        {
            day.RuleFor(item => item.DayNumber).InclusiveBetween(1, 31);
            day.RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class TrainerMemberProgramHandlers :
    IRequestHandler<ListMemberProgramsQuery, Result<PageResult<ProgramResponse>>>,
    IRequestHandler<GetMemberProgramQuery, Result<ProgramResponse>>,
    IRequestHandler<CreateMemberProgramCommand, Result<ProgramResponse>>,
    IRequestHandler<UpdateMemberProgramCommand, Result<ProgramResponse>>,
    IRequestHandler<DeleteMemberProgramCommand, Result<Guid>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerMobileStore _store;
    private readonly IFitnessRepository _fitness;
    private readonly IWorkoutRepository _workouts;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public TrainerMemberProgramHandlers(
        ICurrentActor actor,
        ITrainerMobileStore store,
        IFitnessRepository fitness,
        IWorkoutRepository workouts,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _actor = actor;
        _store = store;
        _fitness = fitness;
        _workouts = workouts;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PageResult<ProgramResponse>>> Handle(ListMemberProgramsQuery request, CancellationToken cancellationToken)
    {
        var member = await OwnedMember(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<PageResult<ProgramResponse>>(member.Error);
        }

        var programs = await _workouts.ListProgramsAsync(member.Value.TenantId, member.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(programs);
    }

    public async Task<Result<ProgramResponse>> Handle(GetMemberProgramQuery request, CancellationToken cancellationToken)
    {
        var member = await OwnedMember(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<ProgramResponse>(member.Error);
        }

        var program = await _workouts.GetProgramAsync(member.Value.TenantId, member.Value.UserId, request.ProgramId, cancellationToken).ConfigureAwait(false);
        return program is null ? Result.Failure<ProgramResponse>(GymErrors.NotFound) : Result.Success(program);
    }

    public async Task<Result<ProgramResponse>> Handle(CreateMemberProgramCommand request, CancellationToken cancellationToken)
    {
        var member = await OwnedMember(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<ProgramResponse>(member.Error);
        }

        var exercises = await ValidateExercises(member.Value.TenantId, request.Days, cancellationToken).ConfigureAwait(false);
        if (exercises.IsFailure)
        {
            return Result.Failure<ProgramResponse>(exercises.Error);
        }

        ProgramResponse? created = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var program = WorkoutProgram.Create(Guid.NewGuid(), member.Value.TenantId, member.Value.UserId, request.Name, request.Description, request.GoalType, request.Difficulty, request.DurationWeeks, request.FrequencyPerWeek);
            await _workouts.AddProgramAsync(program, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            await WriteDays(program.Id, request.Days, token).ConfigureAwait(false);
            await Audit("MemberProgramCreated", program.Id, request.MemberId, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            created = await _workouts.GetProgramAsync(member.Value.TenantId, member.Value.UserId, program.Id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return created is null ? Result.Failure<ProgramResponse>(GymErrors.NotFound) : Result.Success(created);
    }

    public async Task<Result<ProgramResponse>> Handle(UpdateMemberProgramCommand request, CancellationToken cancellationToken)
    {
        var member = await OwnedMember(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<ProgramResponse>(member.Error);
        }

        var exercises = await ValidateExercises(member.Value.TenantId, request.Days, cancellationToken).ConfigureAwait(false);
        if (exercises.IsFailure)
        {
            return Result.Failure<ProgramResponse>(exercises.Error);
        }

        var entity = await _workouts.GetProgramEntityAsync(member.Value.TenantId, member.Value.UserId, request.ProgramId, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.UserId != member.Value.UserId)
        {
            return Result.Failure<ProgramResponse>(GymErrors.NotFound);
        }

        ProgramResponse? updated = null;
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            entity.Update(request.Name, request.Description, request.GoalType, request.Difficulty, request.DurationWeeks, request.FrequencyPerWeek);
            await _workouts.ReplaceProgramDaysAsync(entity.Id, _clock.UtcNow, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            await WriteDays(entity.Id, request.Days, token).ConfigureAwait(false);
            await Audit("MemberProgramUpdated", entity.Id, request.MemberId, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            updated = await _workouts.GetProgramAsync(member.Value.TenantId, member.Value.UserId, entity.Id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return updated is null ? Result.Failure<ProgramResponse>(GymErrors.NotFound) : Result.Success(updated);
    }

    public async Task<Result<Guid>> Handle(DeleteMemberProgramCommand request, CancellationToken cancellationToken)
    {
        var member = await OwnedMember(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<Guid>(member.Error);
        }

        var entity = await _workouts.GetProgramEntityAsync(member.Value.TenantId, member.Value.UserId, request.ProgramId, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.UserId != member.Value.UserId)
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _workouts.SoftDeleteProgramAsync(entity.Id, _clock.UtcNow, token).ConfigureAwait(false);
            await Audit("MemberProgramDeleted", entity.Id, request.MemberId, token).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return Result.Success(entity.Id);
    }

    private async Task WriteDays(Guid programId, IReadOnlyList<ProgramDayInput> days, CancellationToken cancellationToken)
    {
        foreach (var dayInput in days.OrderBy(day => day.DayNumber))
        {
            var day = WorkoutProgramDay.Create(Guid.NewGuid(), programId, dayInput.DayNumber, dayInput.Name, dayInput.Description, dayInput.RestDay);
            await _workouts.AddDayAsync(day, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (dayInput.RestDay)
            {
                continue;
            }

            var sequence = 1;
            foreach (var item in dayInput.Exercises)
            {
                await _workouts.AddProgramExerciseAsync(
                    WorkoutProgramExercise.Create(Guid.NewGuid(), day.Id, item.ExerciseId, sequence++, item.Sets, item.TargetReps, item.TargetDurationSeconds, item.TargetDistance, item.TargetWeight, item.RestSeconds, item.Notes),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<Result> ValidateExercises(Guid tenantId, IReadOnlyList<ProgramDayInput> days, CancellationToken cancellationToken)
    {
        foreach (var item in days.SelectMany(day => day.RestDay ? [] : day.Exercises))
        {
            if (await _fitness.GetExerciseAsync(tenantId, item.ExerciseId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(FitnessErrors.NotFound);
            }
        }

        return Result.Success();
    }

    private async Task<Result<MemberProgramScope>> OwnedMember(Guid memberId, CancellationToken cancellationToken)
    {
        var assigned = await Assigned(memberId, cancellationToken).ConfigureAwait(false);
        if (assigned.IsFailure)
        {
            return Result.Failure<MemberProgramScope>(assigned.Error);
        }

        var member = await _store.GetMemberAsync(assigned.Value.TenantId, memberId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.UserId is not Guid userId)
        {
            return Result.Failure<MemberProgramScope>(GymErrors.NotFound);
        }

        return Result.Success(new MemberProgramScope(assigned.Value.TenantId, assigned.Value.TrainerId, userId));
    }

    private async Task<Result<TrainerProgramScope>> Assigned(Guid memberId, CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<TrainerProgramScope>(IdentityErrors.Forbidden);
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (trainer is null || !await _store.IsAssignedAsync(tenantId, trainer.Id, memberId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TrainerProgramScope>(trainer is null ? IdentityErrors.Forbidden : GymErrors.NotFound);
        }

        return Result.Success(new TrainerProgramScope(tenantId, trainer.Id));
    }

    private Task Audit(string action, Guid programId, Guid memberId, CancellationToken cancellationToken) =>
        _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), _actor.TenantId, _actor.UserId, action, "WorkoutProgram", programId, null, memberId.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken);

    private readonly record struct TrainerProgramScope(Guid TenantId, Guid TrainerId);

    private readonly record struct MemberProgramScope(Guid TenantId, Guid TrainerId, Guid UserId);
}
