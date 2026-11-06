using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Trainers;

public sealed class ScheduleMyTrainerSessionCommandValidator : AbstractValidator<ScheduleMyTrainerSessionCommand>
{
    public ScheduleMyTrainerSessionCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.SessionType).MaximumLength(80);
        RuleFor(command => command.Location).MaximumLength(120);
        RuleFor(command => command.Focus).MaximumLength(500);
    }
}

public sealed class AddMyTrainerSessionExerciseCommandValidator : AbstractValidator<AddMyTrainerSessionExerciseCommand>
{
    public AddMyTrainerSessionExerciseCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.MuscleGroup).MaximumLength(80);
        RuleFor(command => command.Sets).InclusiveBetween(1, 20);
        RuleFor(command => command.Reps).NotEmpty().MaximumLength(40);
    }
}

public sealed class CompleteMyTrainerSessionCommandValidator : AbstractValidator<CompleteMyTrainerSessionCommand>
{
    public CompleteMyTrainerSessionCommandValidator()
    {
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.ElapsedSeconds).InclusiveBetween(0, 28800);
    }
}

public sealed class TrainerMobileCommandHandlers :
    IRequestHandler<ScheduleMyTrainerSessionCommand, Result<Guid>>,
    IRequestHandler<RescheduleMyTrainerSessionCommand, Result<Guid>>,
    IRequestHandler<StartMyTrainerSessionCommand, Result<Guid>>,
    IRequestHandler<RememberMyTrainerSessionCommand, Result<Guid>>,
    IRequestHandler<AddMyTrainerSessionExerciseCommand, Result<Guid>>,
    IRequestHandler<ToggleMyTrainerSessionExerciseCommand, Result<Guid>>,
    IRequestHandler<CompleteMyTrainerSessionCommand, Result<Guid>>,
    IRequestHandler<CancelMyTrainerSessionCommand, Result<Guid>>
{
    private readonly ICurrentActor _actor;
    private readonly ITrainerMobileStore _store;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public TrainerMobileCommandHandlers(ICurrentActor actor, ITrainerMobileStore store, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _actor = actor;
        _store = store;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(ScheduleMyTrainerSessionCommand request, CancellationToken cancellationToken)
    {
        var owned = await Own(cancellationToken).ConfigureAwait(false);
        if (owned is null)
        {
            return Result.Failure<Guid>(IdentityErrors.Forbidden);
        }

        var day = DateOnly.FromDateTime(request.StartUtc);
        if (!await _store.IsAssignedAsync(owned.Value.TenantId, owned.Value.Trainer.Id, request.MemberId, day, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        var session = PtSession.Schedule(Guid.NewGuid(), owned.Value.TenantId, request.MemberId, owned.Value.Trainer.Id, request.StartUtc, request.EndUtc, request.SessionType, request.Location, request.Focus);
        if (session is null)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        await _store.AddSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(session.Id);
    }

    public Task<Result<Guid>> Handle(RescheduleMyTrainerSessionCommand request, CancellationToken cancellationToken) =>
        Change(request.SessionId, session => session.Reschedule(request.StartUtc, request.EndUtc), cancellationToken);

    public Task<Result<Guid>> Handle(StartMyTrainerSessionCommand request, CancellationToken cancellationToken) =>
        Change(request.SessionId, session => session.Start(_clock.UtcNow), cancellationToken);

    public Task<Result<Guid>> Handle(RememberMyTrainerSessionCommand request, CancellationToken cancellationToken) =>
        Change(request.SessionId, session => session.RememberElapsed(request.ElapsedSeconds), cancellationToken);

    public async Task<Result<Guid>> Handle(AddMyTrainerSessionExerciseCommand request, CancellationToken cancellationToken)
    {
        var session = await OwnSession(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session.IsFailure)
        {
            return Result.Failure<Guid>(session.Error);
        }

        var order = await _store.NextExerciseOrderAsync(session.Value.TenantId, session.Value.Id, cancellationToken).ConfigureAwait(false);
        var exercise = PtSessionExercise.Create(Guid.NewGuid(), session.Value, request.Name, request.MuscleGroup, request.Sets, request.Reps, order);
        if (exercise is null)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        await _store.AddExerciseAsync(exercise, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(exercise.Id);
    }

    public async Task<Result<Guid>> Handle(ToggleMyTrainerSessionExerciseCommand request, CancellationToken cancellationToken)
    {
        var session = await OwnSession(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session.IsFailure)
        {
            return Result.Failure<Guid>(session.Error);
        }

        var exercise = await _store.GetExerciseAsync(session.Value.TenantId, session.Value.Id, request.ExerciseId, cancellationToken).ConfigureAwait(false);
        if (exercise is null || !exercise.Toggle(session.Value))
        {
            return Result.Failure<Guid>(exercise is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(exercise.Id);
    }

    public async Task<Result<Guid>> Handle(CompleteMyTrainerSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await OwnSession(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session.IsFailure)
        {
            return Result.Failure<Guid>(session.Error);
        }

        if (session.Value.Status == PtSessionStatus.Completed || await _store.SessionConsumedAsync(session.Value.TenantId, session.Value.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(session.Value.Id);
        }

        var day = DateOnly.FromDateTime(_clock.UtcNow);
        if (!await _store.IsAssignedAsync(session.Value.TenantId, session.Value.TrainerProfileId, session.Value.MemberProfileId, day, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Guid>(GymErrors.NotFound);
        }

        if (!session.Value.Finish(day, _clock.UtcNow, request.Notes, request.ElapsedSeconds))
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        var consumption = await Consume(session.Value, day, cancellationToken).ConfigureAwait(false);
        if (consumption is null)
        {
            return Result.Failure<Guid>(GymErrors.InvalidState);
        }

        await _store.AddConsumptionAsync(consumption, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(session.Value.Id);
    }

    public Task<Result<Guid>> Handle(CancelMyTrainerSessionCommand request, CancellationToken cancellationToken) =>
        Change(request.SessionId, session => session.Cancel(), cancellationToken);

    private async Task<PtSessionConsumption?> Consume(PtSession session, DateOnly day, CancellationToken cancellationToken)
    {
        var packages = await _store.OpenPackagesAsync(session.TenantId, session.TrainerProfileId, session.MemberProfileId, day, cancellationToken).ConfigureAwait(false);
        foreach (var package in packages)
        {
            if (package.TryConsume(day))
            {
                return PtSessionConsumption.ForPackage(Guid.NewGuid(), session, package.Id, _clock.UtcNow);
            }
        }

        var memberships = await _store.OpenMembershipsAsync(session.TenantId, session.MemberProfileId, cancellationToken).ConfigureAwait(false);
        foreach (var membership in memberships)
        {
            if (membership.TryConsumeIncludedSession(day))
            {
                return PtSessionConsumption.ForMembership(Guid.NewGuid(), session, membership.Id, _clock.UtcNow);
            }
        }

        return null;
    }

    private async Task<Result<Guid>> Change(Guid sessionId, Func<PtSession, bool> change, CancellationToken cancellationToken)
    {
        var session = await OwnSession(sessionId, cancellationToken).ConfigureAwait(false);
        if (session.IsFailure || !change(session.Value))
        {
            return Result.Failure<Guid>(session.IsFailure ? session.Error : GymErrors.InvalidState);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(session.Value.Id);
    }

    private async Task<Result<PtSession>> OwnSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var owned = await Own(cancellationToken).ConfigureAwait(false);
        if (owned is null)
        {
            return Result.Failure<PtSession>(IdentityErrors.Forbidden);
        }

        var session = await _store.GetSessionAsync(owned.Value.TenantId, owned.Value.Trainer.Id, sessionId, cancellationToken).ConfigureAwait(false);
        return session is null ? Result.Failure<PtSession>(GymErrors.NotFound) : Result.Success(session);
    }

    private async Task<(Guid TenantId, TrainerProfile Trainer)?> Own(CancellationToken cancellationToken)
    {
        if (_actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return null;
        }

        var trainer = await _store.GetTrainerByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        return trainer is null ? null : (tenantId, trainer);
    }
}
