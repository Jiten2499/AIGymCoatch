using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Memberships;

public sealed record CreateMembershipCommand(Guid MemberProfileId, Guid BranchId, Guid PlanId, DateOnly StartDate, bool AutoRenew, string? Notes) : IRequest<Result<GymMembershipResponse>>;

public sealed class CreateMembershipCommandValidator : AbstractValidator<CreateMembershipCommand>
{
    public CreateMembershipCommandValidator()
    {
        RuleFor(command => command.MemberProfileId).NotEmpty();
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(1000);
    }
}

public sealed class CreateMembershipCommandHandler : IRequestHandler<CreateMembershipCommand, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IMembershipRepository _memberships;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public CreateMembershipCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IBranchRepository branches, IMembershipRepository memberships, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _branches = branches;
        _memberships = memberships;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<GymMembershipResponse>> Handle(CreateMembershipCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipCreate, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<GymMembershipResponse>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, request.MemberProfileId, cancellationToken).ConfigureAwait(false);
        var branch = await _branches.GetAsync(tenantId, request.BranchId, cancellationToken).ConfigureAwait(false);
        var plan = await _memberships.GetPlanAsync(tenantId, request.PlanId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.Status != ProfileStatus.Active || branch is null || branch.Status != BranchStatus.Active || plan is null || !plan.IsActive)
        {
            return Result.Failure<GymMembershipResponse>(GymErrors.NotFound);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var membership = GymMembership.Create(Guid.NewGuid(), tenantId, member.Id, branch.Id, plan.Id, $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.StartDate, plan.DurationDays, today, request.AutoRenew, request.Notes);
        await _memberships.AddMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
        await _memberships.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, MembershipAction.Created, null, membership.Status, request.StartDate, request.Notes, _actor.UserId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "CreateMembership", "GymMembership", membership.Id, null, membership.MembershipNumber, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(membership));
    }

    internal static GymMembershipResponse Map(GymMembership membership) =>
        new(membership.Id, membership.MemberProfileId, membership.BranchId, membership.MembershipPlanId, membership.MembershipNumber, membership.StartDate, membership.EndDate, membership.Status, membership.AutoRenew, membership.Notes);
}

public sealed record UpdatePlanCommand(Guid Id, string Name, string? Description, int DurationDays, MembershipType MembershipType, int? MaxVisits, decimal Price, string Currency) : IRequest<Result<PlanResponse>>;

public sealed class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DurationDays).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
    }
}

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, Result<PlanResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _unitOfWork = unitOfWork;
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

        plan.Update(request.Name.Trim(), request.Description, request.DurationDays, request.MembershipType, request.MaxVisits, request.Price, request.Currency.ToUpperInvariant());
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(CreatePlanCommandHandler.Map(plan));
    }
}

public sealed record GetMembershipQuery(Guid Id) : IRequest<Result<GymMembershipResponse>>;

public sealed class GetMembershipQueryHandler : IRequestHandler<GetMembershipQuery, Result<GymMembershipResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;

    public GetMembershipQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
    }

    public async Task<Result<GymMembershipResponse>> Handle(GetMembershipQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<GymMembershipResponse>(gate.Error);
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return membership is null
            ? Result.Failure<GymMembershipResponse>(GymErrors.NotFound)
            : Result.Success(CreateMembershipCommandHandler.Map(membership));
    }
}

public sealed record ListMembershipHistoryQuery(Guid MembershipId) : IRequest<Result<IReadOnlyList<MembershipHistoryResponse>>>;

public sealed class ListMembershipHistoryQueryHandler : IRequestHandler<ListMembershipHistoryQuery, Result<IReadOnlyList<MembershipHistoryResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;

    public ListMembershipHistoryQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
    }

    public async Task<Result<IReadOnlyList<MembershipHistoryResponse>>> Handle(ListMembershipHistoryQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<MembershipHistoryResponse>>(gate.Error);
        }

        if (await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<MembershipHistoryResponse>>(GymErrors.NotFound);
        }

        return Result.Success(await _memberships.ListHistoryAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false));
    }
}
