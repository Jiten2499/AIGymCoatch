using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym;

public sealed record ListStaffQuery(int Page, int PageSize) : IRequest<Result<PageResult<StaffResponse>>>;

public sealed class ListStaffQueryHandler : IRequestHandler<ListStaffQuery, Result<PageResult<StaffResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;

    public ListStaffQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
    }

    public async Task<Result<PageResult<StaffResponse>>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StaffRead, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<PageResult<StaffResponse>>(gate.Error)
            : Result.Success(await _people.ListStaffAsync(tenantId, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record ListTrainersQuery(int Page, int PageSize, string? Search = null, string? Status = null, string? Specialization = null) : IRequest<Result<PageResult<TrainerResponse>>>;

public sealed class ListTrainersQueryHandler : IRequestHandler<ListTrainersQuery, Result<PageResult<TrainerResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;

    public ListTrainersQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
    }

    public async Task<Result<PageResult<TrainerResponse>>> Handle(ListTrainersQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerRead, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<PageResult<TrainerResponse>>(gate.Error)
            : Result.Success(await _people.ListTrainersAsync(tenantId, new PageQuery(request.Page, request.PageSize), request.Search, request.Status, request.Specialization, cancellationToken).ConfigureAwait(false));
    }
}

public sealed record UpdateTrainerCommand(Guid Id, string? Bio, string? Specialization, int? ExperienceYears, string? Certification, string? ProfileImageUrl, Guid? BranchId, ProfileStatus Status, string? FirstName = null, string? LastName = null, string? Phone = null, bool SetPhone = false, string? EmployeeCode = null, DateOnly? JoiningDate = null, bool CommissionEligible = false, bool SetEmployment = false) : IRequest<Result<TrainerResponse>>;

public sealed class UpdateTrainerCommandHandler : IRequestHandler<UpdateTrainerCommand, Result<TrainerResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTrainerCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users, IUnitOfWork unitOfWork)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TrainerResponse>> Handle(UpdateTrainerCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.TrainerUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<TrainerResponse>(gate.Error);
        }

        var trainer = await _people.GetTrainerAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var user = trainer is null ? null : await _users.GetByIdAsync(trainer.UserId, cancellationToken).ConfigureAwait(false);
        if (trainer is null || user is null)
        {
            return Result.Failure<TrainerResponse>(GymErrors.NotFound);
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName))
        {
            user.Rename(request.FirstName, request.LastName);
        }

        if (request.SetPhone)
        {
            user.ChangePhone(request.Phone);
        }

        trainer.Update(request.Bio, request.Specialization, request.ExperienceYears, request.Certification, request.ProfileImageUrl, request.BranchId, request.Status);
        if (request.SetEmployment)
        {
            trainer.SetEmployment(request.EmployeeCode, request.JoiningDate, request.CommissionEligible);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(TrainerMaps.ToResponse(trainer, user));
    }
}

public sealed record ListPlansQuery(bool? ActiveOnly, string? Term, PackageCategory? Category, int Page, int PageSize) : IRequest<Result<PageResult<PlanResponse>>>;

public sealed class ListPlansQueryHandler : IRequestHandler<ListPlansQuery, Result<PageResult<PlanResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;

    public ListPlansQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
    }

    public async Task<Result<PageResult<PlanResponse>>> Handle(ListPlansQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<PageResult<PlanResponse>>(gate.Error)
            : Result.Success(await _plans.ListPlansAsync(tenantId, request.ActiveOnly, request.Term, request.Category, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record DeactivatePlanCommand(Guid Id) : IRequest<Result>;

public sealed class DeactivatePlanCommandHandler : IRequestHandler<DeactivatePlanCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public DeactivatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(DeactivatePlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return gate;
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        plan.Deactivate();
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageDeactivated", "MembershipPlan", plan.Id, "Active", "Inactive", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record ActivatePlanCommand(Guid Id) : IRequest<Result>;

public sealed class ActivatePlanCommandHandler : IRequestHandler<ActivatePlanCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _plans;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ActivatePlanCommandHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository plans, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _plans = plans;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(ActivatePlanCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipPlanManage, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return gate;
        }

        var plan = await _plans.GetPlanAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        plan.Activate();
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "PackageActivated", "MembershipPlan", plan.Id, "Inactive", "Active", _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record ListMembershipsQuery(GymMembershipStatus? Status, Guid? PlanId, Guid? BranchId, DateOnly? ExpiresFrom, DateOnly? ExpiresTo, Guid? MemberProfileId, string? Term, DateOnly? StartsFrom, DateOnly? StartsTo, string? Sort, int Page, int PageSize) : IRequest<Result<PageResult<GymMembershipResponse>>>;

public sealed class ListMembershipsQueryHandler : IRequestHandler<ListMembershipsQuery, Result<PageResult<GymMembershipResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IDateTimeProvider _clock;

    public ListMembershipsQueryHandler(IOperationGuard guard, ICurrentActor actor, IMembershipRepository memberships, IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _clock = clock;
    }

    public async Task<Result<PageResult<GymMembershipResponse>>> Handle(ListMembershipsQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MembershipRead, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<PageResult<GymMembershipResponse>>(gate.Error)
            : Result.Success(await _memberships.SearchMembershipsAsync(tenantId, request.Status, request.PlanId, request.BranchId, request.MemberProfileId, request.Term, request.StartsFrom, request.StartsTo, request.ExpiresFrom, request.ExpiresTo, DateOnly.FromDateTime(_clock.UtcNow), request.Sort, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record ListAttendanceQuery(Guid? BranchId, Guid? MemberProfileId, DateTime? From, DateTime? To, int Page, int PageSize) : IRequest<Result<PageResult<AttendanceResponse>>>;

public sealed class ListAttendanceQueryHandler : IRequestHandler<ListAttendanceQuery, Result<PageResult<AttendanceResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAttendanceRepository _attendance;

    public ListAttendanceQueryHandler(IOperationGuard guard, ICurrentActor actor, IAttendanceRepository attendance)
    {
        _guard = guard;
        _actor = actor;
        _attendance = attendance;
    }

    public async Task<Result<PageResult<AttendanceResponse>>> Handle(ListAttendanceQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.AttendanceRead, FeatureCodes.Attendance, cancellationToken).ConfigureAwait(false);
        return gate.IsFailure || _actor.TenantId is not Guid tenantId
            ? Result.Failure<PageResult<AttendanceResponse>>(gate.Error)
            : Result.Success(await _attendance.ListAsync(tenantId, request.BranchId, request.MemberProfileId, request.From, request.To, new PageQuery(request.Page, request.PageSize), cancellationToken).ConfigureAwait(false));
    }
}
