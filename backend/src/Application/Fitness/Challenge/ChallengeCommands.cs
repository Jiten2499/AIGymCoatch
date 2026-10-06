using FluentValidation;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Fitness.Challenges;

public sealed record ListChallengesQuery(int Page, int PageSize) : IRequest<Result<PageResult<ChallengeResponse>>>;

public sealed class ListChallengesQueryHandler : IRequestHandler<ListChallengesQuery, Result<PageResult<ChallengeResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IChallengeRepository _challenges;

    public ListChallengesQueryHandler(IOperationGuard guard, ICurrentActor actor, IChallengeRepository challenges)
    {
        _guard = guard;
        _actor = actor;
        _challenges = challenges;
    }

    public async Task<Result<PageResult<ChallengeResponse>>> Handle(ListChallengesQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ChallengeRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<ChallengeResponse>>(scope.Error);
        }

        return Result.Success(await _challenges.ListAsync(scope.Value.TenantId, scope.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record CreateChallengeCommand(string Name, string? Description, ChallengeType ChallengeType, DateOnly StartDate, DateOnly EndDate, decimal TargetValue) : IRequest<Result<ChallengeResponse>>;

public sealed class CreateChallengeCommandValidator : AbstractValidator<CreateChallengeCommand>
{
    public CreateChallengeCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.TargetValue).GreaterThan(0);
        RuleFor(command => command.EndDate).GreaterThanOrEqualTo(command => command.StartDate);
    }
}

public sealed class CreateChallengeCommandHandler : IRequestHandler<CreateChallengeCommand, Result<ChallengeResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IChallengeRepository _challenges;
    private readonly IUnitOfWork _unitOfWork;

    public CreateChallengeCommandHandler(IOperationGuard guard, ICurrentActor actor, IChallengeRepository challenges, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _challenges = challenges;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ChallengeResponse>> Handle(CreateChallengeCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ChallengeManage, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChallengeResponse>(scope.Error);
        }

        var challenge = Domain.Fitness.Challenge.Create(Guid.NewGuid(), scope.Value.TenantId, request.Name, request.Description, request.ChallengeType, request.StartDate, request.EndDate, request.TargetValue);
        await _challenges.AddAsync(challenge, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ChallengeResponse(challenge.Id, challenge.Name, challenge.Description, challenge.ChallengeType, challenge.StartDate, challenge.EndDate, challenge.TargetValue, challenge.Status, false, null, null));
    }
}

public sealed record JoinChallengeCommand(Guid Id) : IRequest<Result<ChallengeResponse>>;

public sealed record RecordChallengeProgressCommand(Guid Id, decimal Value) : IRequest<Result<ChallengeResponse>>;

public sealed class ChallengeProgressHandler :
    IRequestHandler<JoinChallengeCommand, Result<ChallengeResponse>>,
    IRequestHandler<RecordChallengeProgressCommand, Result<ChallengeResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IChallengeRepository _challenges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ChallengeProgressHandler(IOperationGuard guard, ICurrentActor actor, IChallengeRepository challenges, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _challenges = challenges;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ChallengeResponse>> Handle(JoinChallengeCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ChallengeRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChallengeResponse>(scope.Error);
        }

        var challenge = await _challenges.GetVisibleAsync(scope.Value.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (challenge is null || challenge.Status != ChallengeStatus.Active)
        {
            return Result.Failure<ChallengeResponse>(FitnessErrors.NotFound);
        }

        if (await _challenges.GetParticipantAsync(challenge.Id, scope.Value.UserId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<ChallengeResponse>(FitnessErrors.InvalidState);
        }

        await _challenges.AddParticipantAsync(ChallengeParticipant.Join(Guid.NewGuid(), challenge.Id, scope.Value.UserId, scope.Value.TenantId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _challenges.AddNotificationAsync(Notification.Create(Guid.NewGuid(), scope.Value.UserId, scope.Value.TenantId, NotificationType.ChallengeReminder, "Challenge joined", challenge.Name, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ChallengeResponse(challenge.Id, challenge.Name, challenge.Description, challenge.ChallengeType, challenge.StartDate, challenge.EndDate, challenge.TargetValue, challenge.Status, challenge.IsSystem, 0, null));
    }

    public async Task<Result<ChallengeResponse>> Handle(RecordChallengeProgressCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.ChallengeRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChallengeResponse>(scope.Error);
        }

        var challenge = await _challenges.GetVisibleAsync(scope.Value.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var participant = challenge is null ? null : await _challenges.GetParticipantAsync(challenge.Id, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (challenge is null || participant is null || participant.TenantId != scope.Value.TenantId)
        {
            return Result.Failure<ChallengeResponse>(FitnessErrors.NotFound);
        }

        participant.Record(request.Value, challenge.TargetValue, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ChallengeResponse(challenge.Id, challenge.Name, challenge.Description, challenge.ChallengeType, challenge.StartDate, challenge.EndDate, challenge.TargetValue, challenge.Status, challenge.IsSystem, participant.CurrentValue, participant.CompletedAt));
    }
}

public sealed record ListNotificationsQuery(int Page, int PageSize) : IRequest<Result<PageResult<NotificationResponse>>>;

public sealed record MarkNotificationReadCommand(Guid Id) : IRequest<Result<NotificationResponse>>;

public sealed class NotificationHandler :
    IRequestHandler<ListNotificationsQuery, Result<PageResult<NotificationResponse>>>,
    IRequestHandler<MarkNotificationReadCommand, Result<NotificationResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IChallengeRepository _challenges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public NotificationHandler(IOperationGuard guard, ICurrentActor actor, IChallengeRepository challenges, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _challenges = challenges;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<PageResult<NotificationResponse>>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NotificationRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<NotificationResponse>>(scope.Error);
        }

        var page = await _challenges.ListNotificationsAsync(scope.Value.TenantId, scope.Value.UserId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false);
        return Result.Success(new PageResult<NotificationResponse>(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<Result<NotificationResponse>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var scope = await Scope.Open(_guard, _actor, PermissionCodes.NotificationRead, FeatureCodes.WorkoutPlans, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<NotificationResponse>(scope.Error);
        }

        var notification = await _challenges.GetNotificationAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, cancellationToken).ConfigureAwait(false);
        if (notification is null)
        {
            return Result.Failure<NotificationResponse>(FitnessErrors.NotFound);
        }

        notification.MarkRead(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(notification));
    }

    private static NotificationResponse Map(Domain.Fitness.Notification notification) =>
        new(notification.Id, notification.Type, notification.Title, notification.Message, notification.IsRead, notification.CreatedAt);
}
