using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed class CreatePlanCommandHandler : IRequestHandler<CreatePlanCommand, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _profiles = profiles;
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
        var currency = await WorkspaceCurrency.ForTenantAsync(_profiles, tenantId, request.Currency, cancellationToken).ConfigureAwait(false);
        var plan = MembershipPlan.Create(Guid.NewGuid(), tenantId, request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, currency, request.Category, duration.Unit, duration.Count, request.IncludedPtSessions);
        await _plans.AddPlanAsync(plan, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageCreated", "MembershipPlan", plan.Id, null, plan.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(plan));
    }

    internal static PlanResponse Map(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.Description, plan.DurationDays, plan.MembershipType, plan.MaxVisits, plan.Price, plan.Currency, plan.IsActive, plan.Category, plan.DurationUnit, plan.DurationCount, plan.IncludedPtSessions);
}

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IBusinessProfileRepository profiles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _profiles = profiles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<PlanResponse>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PlanResponse>(gate.Error);
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure<PlanResponse>(GymErrors.NotFound);
        }

        var duration = MembershipCalendar.Resolve(request.MembershipType, request.DurationDays, request.DurationUnit, request.DurationCount);
        var currency = await WorkspaceCurrency.ForTenantAsync(_profiles, tenantId, request.Currency, cancellationToken).ConfigureAwait(false);
        plan.Update(request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, currency, request.Category, duration.Unit, duration.Count, request.IncludedPtSessions);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageUpdated", "MembershipPlan", plan.Id, null, plan.Name, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(CreatePlanCommandHandler.Map(plan));
    }
}
