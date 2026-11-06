using FluentValidation;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record CreatePlanCommand(string Name, string? Description, int DurationDays, MembershipType MembershipType, int? MaxVisits, decimal Price, string Currency, PackageCategory Category = PackageCategory.Standard, PackageDurationUnit DurationUnit = PackageDurationUnit.Day, int? DurationCount = null, int IncludedPtSessions = 0) : IRequest<Result<PlanResponse>>;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DurationDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.MaxVisits).GreaterThan(0).When(command => command.MaxVisits is not null);
        RuleFor(command => command.IncludedPtSessions).GreaterThanOrEqualTo(0);
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

        var duration = MembershipCalendar.Resolve(request.MembershipType, request.DurationDays, request.DurationUnit, request.DurationCount);
        var plan = MembershipPlan.Create(Guid.NewGuid(), tenantId, request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency.ToUpperInvariant(), request.Category, duration.Unit, duration.Count, request.IncludedPtSessions);
        await _plans.AddPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageCreated", "MembershipPlan", plan.Id, null, plan.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(plan));
    }

    internal static PlanResponse Map(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.Description, plan.DurationDays, plan.MembershipType, plan.MaxVisits, plan.Price, plan.Currency, plan.IsActive, plan.Category, plan.DurationUnit, plan.DurationCount, plan.IncludedPtSessions);
}

public sealed record ChangeMembershipCommand(Guid Id, string Action, string? Reason, bool AutoRenew, int Days = 0) : IRequest<Result<GymMembershipResponse>>;

public sealed class ChangeMembershipCommandHandler : IRequestHandler<ChangeMembershipCommand, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IBusinessConfigRepository _configs;
    private readonly IMembershipApprovalQueue _approvals;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ChangeMembershipCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IBusinessConfigRepository configs, IMembershipApprovalQueue approvals, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _configs = configs;
        _approvals = approvals;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<GymMembershipResponse>> Handle(ChangeMembershipCommand request, CancellationToken cancellationToken)
    {
        var permission = request.Action switch
        {
            "renew" or "extend" => PermissionCodes.MembershipRenew,
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
        if (request.Action == "renew")
        {
            return await RenewAsync(membership, tenantId, today, request, cancellationToken).ConfigureAwait(false);
        }

        if (request.Action == "freeze")
        {
            return await FreezeAsync(membership, tenantId, today, previous, request, cancellationToken).ConfigureAwait(false);
        }

        if (request.Action == "unfreeze")
        {
            return await UnfreezeAsync(membership, tenantId, today, previous, request.Reason, cancellationToken).ConfigureAwait(false);
        }

        if (request.Action == "extend")
        {
            var extensionConfig = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (extensionConfig is { ExtensionRequiresApproval: true })
            {
                if (request.Days <= 0 || (extensionConfig.MaxExtensionDays > 0 && request.Days > extensionConfig.MaxExtensionDays))
                {
                    return Result.Failure<GymMembershipResponse>(GymErrors.ExtensionOutsideLimits);
                }

                if (_actor.UserId is not Guid actorId)
                {
                    return Result.Failure<GymMembershipResponse>(IdentityErrors.Forbidden);
                }

                var queued = await _approvals.QueueExtensionAsync(tenantId, actorId, membership, request.Days, request.Reason, cancellationToken).ConfigureAwait(false);
                return queued.IsFailure
                    ? Result.Failure<GymMembershipResponse>(queued.Error)
                    : Result.Success(CreateMembershipCommandHandler.Map(membership, today) with { ApprovalRequestId = queued.Value });
            }
        }

        var changed = request.Action switch
        {
            "extend" => membership.Extend(request.Days),
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
            "extend" => MembershipAction.Extended,
            "cancel" => MembershipAction.Cancelled,
            _ => MembershipAction.Expired,
        };
        await RecordAsync(tenantId, membership, action, previous, today, request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(CreateMembershipCommandHandler.Map(membership, today));
    }

    private async Task<Result<GymMembershipResponse>> RenewAsync(GymMembership membership, Guid tenantId, DateOnly today, ChangeMembershipCommand request, CancellationToken cancellationToken)
    {
        var plan = await _memberships.GetPlanAsync(tenantId, membership.MembershipPlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null || !plan.IsActive)
        {
            return Result.Failure<GymMembershipResponse>(plan is null ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        var successor = membership.ScheduleRenewal(today, plan.DurationCount > 0 ? plan.DurationCount : plan.DurationDays, plan.DurationUnit, Guid.NewGuid(), $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.AutoRenew, request.Reason);
        if (successor is null)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        successor.SetPricing(plan.Price, 0m, plan.Price, plan.Currency, null, null);
        if (await _memberships.OverlapsMembershipAsync(tenantId, membership.MemberProfileId, successor.StartDate, successor.EndDate, membership.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.Conflict);
        }

        await _memberships.AddMembershipAsync(successor, cancellationToken).ConfigureAwait(false);
        await RecordAsync(tenantId, successor, MembershipAction.Renewed, null, today, request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(CreateMembershipCommandHandler.Map(successor, today));
    }

    private async Task<Result<GymMembershipResponse>> FreezeAsync(GymMembership membership, Guid tenantId, DateOnly today, GymMembershipStatus previous, ChangeMembershipCommand request, CancellationToken cancellationToken)
    {
        var config = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var count = await _memberships.CountFreezesAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
        var policy = FreezeRules.Check(config, request.Days, count);
        if (policy is not null)
        {
            return Result.Failure<GymMembershipResponse>(policy);
        }

        if (config is { FreezeRequiresApproval: true })
        {
            if (_actor.UserId is not Guid actorId)
            {
                return Result.Failure<GymMembershipResponse>(IdentityErrors.Forbidden);
            }

            var queued = await _approvals.QueueFreezeAsync(tenantId, actorId, membership, request.Days, request.Reason, cancellationToken).ConfigureAwait(false);
            return queued.IsFailure
                ? Result.Failure<GymMembershipResponse>(queued.Error)
                : Result.Success(CreateMembershipCommandHandler.Map(membership, today) with { ApprovalRequestId = queued.Value });
        }

        if (await _memberships.FreezeOverlapsAsync(tenantId, membership.Id, today, cancellationToken).ConfigureAwait(false) || !membership.Freeze(today))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var planned = request.Days > 0 ? request.Days : (int?)null;
        if (planned is int days && !membership.ExtendForFreeze(days))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        await _memberships.AddFreezeAsync(MembershipFreeze.Open(Guid.NewGuid(), tenantId, membership.Id, today, previous, request.Reason, _clock.UtcNow, planned), cancellationToken).ConfigureAwait(false);
        await RecordAsync(tenantId, membership, MembershipAction.Frozen, previous, today, request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(CreateMembershipCommandHandler.Map(membership, today));
    }

    private async Task<Result<GymMembershipResponse>> UnfreezeAsync(GymMembership membership, Guid tenantId, DateOnly today, GymMembershipStatus previous, string? reason, CancellationToken cancellationToken)
    {
        var freeze = await _memberships.GetOpenFreezeAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
        if (freeze is null)
        {
            if (!membership.Unfreeze(today))
            {
                return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
            }

            await RecordAsync(tenantId, membership, MembershipAction.Unfrozen, previous, today, reason, cancellationToken).ConfigureAwait(false);
            return Result.Success(CreateMembershipCommandHandler.Map(membership, today));
        }

        if (today < freeze.StartDate)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        var frozenDays = today.DayNumber - freeze.StartDate.DayNumber;
        var lastFrozenDay = frozenDays == 0 ? freeze.StartDate : today.AddDays(-1);
        var config = await _configs.GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var extension = FreezeRules.ExtensionDays(config, frozenDays, freeze.PlannedDays);
        if (!freeze.Close(lastFrozenDay) || !membership.ExtendForFreeze(extension) || !membership.Unfreeze(today))
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.InvalidState);
        }

        await RecordAsync(tenantId, membership, MembershipAction.Unfrozen, previous, today, reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(CreateMembershipCommandHandler.Map(membership, today));
    }

    private async Task RecordAsync(Guid tenantId, GymMembership membership, MembershipAction action, GymMembershipStatus? previous, DateOnly today, string? reason, CancellationToken cancellationToken)
    {
        var audit = action switch
        {
            MembershipAction.Renewed => "MembershipRenewed",
            MembershipAction.Frozen => "MembershipFrozen",
            MembershipAction.Unfrozen => "MembershipUnfrozen",
            MembershipAction.Extended => "MembershipExtended",
            MembershipAction.Cancelled => "MembershipCancelled",
            _ => action.ToString(),
        };
        await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, action, previous, membership.Status, today, reason, _actor.UserId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, audit, "GymMembership", membership.Id, previous?.ToString(), membership.Status.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
