using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record CreatePlanCommand(string Name, string? Description, int DurationDays, MembershipType MembershipType, int? MaxVisits, decimal Price, string Currency) : IRequest<Result<PlanResponse>>;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DurationDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.MaxVisits).GreaterThan(0).When(command => command.MaxVisits is not null);
    }
}

public sealed class CreatePlanCommandHandler : IRequestHandler<CreatePlanCommand, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PlanResponse>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PlanResponse>(gate.Error);
        }

        if (await _plans.PlanNameExistsAsync(tenantId, request.Name.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PlanResponse>(GymErrors.Conflict);
        }

        var plan = MembershipPlan.Create(Guid.NewGuid(), tenantId, request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency.ToUpperInvariant());
        await _plans.AddPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "CreatePlan", "MembershipPlan", plan.Id, null, plan.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(plan));
    }

    internal static PlanResponse Map(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.Description, plan.DurationDays, plan.MembershipType, plan.MaxVisits, plan.Price, plan.Currency, plan.IsActive);
}

public sealed record ChangeMembershipCommand(Guid Id, string Action, string? Reason, bool AutoRenew) : IRequest<Result<GymMembershipResponse>>;

public sealed class ChangeMembershipCommandHandler : IRequestHandler<ChangeMembershipCommand, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeMembershipCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<GymMembershipResponse>> Handle(ChangeMembershipCommand request, CancellationToken cancellationToken)
    {
        var permission = request.Action switch
        {
            "renew" => PermissionCodes.MembershipRenew,
            "freeze" or "unfreeze" => PermissionCodes.MembershipFreeze,
            "cancel" or "expire" => PermissionCodes.MembershipCancel,
            _ => null,
        };
        if (permission is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var gate = await _guard.AllowAsync(permission, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<GymMembershipResponse>(gate.Error);
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var previous = membership.Status;
        var plan = await _memberships.GetPlanAsync(tenantId, membership.MembershipPlanId, cancellationToken).ConfigureAwait(false);
        var changed = request.Action switch
        {
            "renew" when plan is not null => membership.Renew(today, plan.DurationDays, request.AutoRenew),
            "freeze" => membership.Freeze(today),
            "unfreeze" => membership.Unfreeze(today),
            "cancel" => membership.Cancel(),
            "expire" => membership.Expire(today),
            _ => false,
        };
        if (!changed)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var action = request.Action switch
        {
            "renew" => MembershipAction.Renewed,
            "freeze" => MembershipAction.Frozen,
            "unfreeze" => MembershipAction.Unfrozen,
            "cancel" => MembershipAction.Cancelled,
            _ => MembershipAction.Expired,
        };
        await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, action, previous, membership.Status, today, request.Reason, _actor.UserId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action.ToString(), "GymMembership", membership.Id, previous.ToString(), membership.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new GymMembershipResponse(membership.Id, membership.MemberProfileId, membership.BranchId, membership.MembershipPlanId, membership.MembershipNumber, membership.StartDate, membership.EndDate, membership.Status, membership.AutoRenew, membership.Notes));
    }
}
