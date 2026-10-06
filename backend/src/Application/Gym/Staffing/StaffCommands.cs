using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Staffing;

public sealed record AddStaffCommand(string Email, string? Password, string FirstName, string LastName, string? Phone, StaffType StaffType, Guid? BranchId) : IRequest<Result<StaffResponse>>;

public sealed class AddStaffCommandValidator : AbstractValidator<AddStaffCommand>
{
    public AddStaffCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.FirstName).NotEmpty();
        RuleFor(command => command.LastName).NotEmpty();
        RuleFor(command => command.StaffType).IsInEnum().NotEqual(StaffType.Other);
    }
}

public sealed class AddStaffCommandHandler : IRequestHandler<AddStaffCommand, Result<StaffResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAccountLinker _accounts;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public AddStaffCommandHandler(IOperationGuard guard, ICurrentActor actor, IAccountLinker accounts, ITenantRepository tenants, ITenantMembershipRepository memberships, IRoleRepository roles, IPeopleRepository people, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _accounts = accounts;
        _tenants = tenants;
        _memberships = memberships;
        _roles = roles;
        _people = people;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<StaffResponse>> Handle(AddStaffCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StaffCreate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<StaffResponse>(gate.Error);
        }

        if (request.BranchId is Guid branchId)
        {
            var branch = await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false);
            if (branch is null || branch.Status != BranchStatus.Active)
            {
                return Result.Failure<StaffResponse>(GymErrors.NotFound);
            }
        }

        var roleName = request.StaffType switch
        {
            StaffType.Trainer => RoleNames.Trainer,
            StaffType.Receptionist => RoleNames.Receptionist,
            StaffType.Manager => RoleNames.GymManager,
            _ => null,
        };
        if (roleName is null)
        {
            return Result.Failure<StaffResponse>(GymErrors.InvalidState);
        }

        StaffResponse? response = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var linked = await _accounts.ResolveAsync(request.Email, request.Password, request.FirstName, request.LastName, request.Phone, ct).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    throw new Members.MemberOnboardingException(linked.Error);
                }

                var user = linked.Value;
                if (await _people.GetStaffByUserAsync(tenantId, user.Id, ct).ConfigureAwait(false) is not null)
                {
                    throw new Members.MemberOnboardingException(GymErrors.Conflict);
                }

                if (await _memberships.GetActiveAsync(tenantId, user.Id, ct).ConfigureAwait(false) is null)
                {
                    await _memberships.AddAsync(TenantMember.Create(Guid.NewGuid(), tenantId, user.Id, _clock.UtcNow), ct).ConfigureAwait(false);
                }

                var role = await _roles.GetByNameAsync(roleName, ct).ConfigureAwait(false) ?? throw new Members.MemberOnboardingException(IdentityErrors.RoleNotFound);
                var names = await _roles.GetRoleNamesAsync(user.Id, tenantId, ct).ConfigureAwait(false);
                if (!names.Contains(roleName))
                {
                    await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, tenantId), ct).ConfigureAwait(false);
                }

                var today = DateOnly.FromDateTime(_clock.UtcNow);
                var staff = StaffProfile.Create(Guid.NewGuid(), tenantId, user.Id, $"S{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.BranchId, request.StaffType, today);
                await _people.AddStaffAsync(staff, ct).ConfigureAwait(false);
                if (request.StaffType == StaffType.Trainer)
                {
                    await _people.AddTrainerAsync(TrainerProfile.Create(Guid.NewGuid(), tenantId, user.Id, request.BranchId), ct).ConfigureAwait(false);
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "AddStaff", "StaffProfile", staff.Id, null, staff.EmployeeCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                response = new StaffResponse(staff.Id, user.Id, staff.EmployeeCode, user.Email, user.FirstName, user.LastName, staff.BranchId, staff.StaffType, staff.JoiningDate, staff.Status);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Members.MemberOnboardingException exception)
        {
            return Result.Failure<StaffResponse>(exception.Error);
        }

        return Result.Success(response!);
    }
}

public sealed record RemoveStaffCommand(Guid Id) : IRequest<Result>;

public sealed class RemoveStaffCommandHandler : IRequestHandler<RemoveStaffCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly ITenantRepository _tenants;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public RemoveStaffCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, ITenantRepository tenants, IRoleRepository roles, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _tenants = tenants;
        _roles = roles;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(RemoveStaffCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StaffRemove, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return gate;
        }

        var staff = await _people.GetStaffAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var tenant = await _tenants.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (staff is null || tenant is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        if (tenant.OwnerUserId == staff.UserId)
        {
            return Result.Failure(IdentityErrors.OwnerRemovalRejected);
        }

        var now = _clock.UtcNow;
        staff.Remove(now);
        var trainer = await _people.GetTrainerByUserAsync(tenantId, staff.UserId, cancellationToken).ConfigureAwait(false);
        trainer?.Remove(now);
        await _roles.RemoveForMemberAsync(staff.UserId, tenantId, now, cancellationToken).ConfigureAwait(false);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "RemoveStaff", "StaffProfile", staff.Id, null, staff.EmployeeCode, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record UpdateStaffCommand(Guid Id, Guid? BranchId, StaffType StaffType, ProfileStatus Status) : IRequest<Result<StaffResponse>>;

public sealed class UpdateStaffCommandHandler : IRequestHandler<UpdateStaffCommand, Result<StaffResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public UpdateStaffCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<StaffResponse>> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StaffUpdate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<StaffResponse>(gate.Error);
        }

        if (request.BranchId is Guid branchId)
        {
            var branch = await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false);
            if (branch is null)
            {
                return Result.Failure<StaffResponse>(GymErrors.NotFound);
            }
        }

        var staff = await _people.GetStaffAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (staff is null)
        {
            return Result.Failure<StaffResponse>(GymErrors.NotFound);
        }

        var now = _clock.UtcNow;
        staff.Update(request.BranchId, request.StaffType, request.Status);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "UpdateStaff", "StaffProfile", staff.Id, null, staff.EmployeeCode, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var user = await _users.GetByIdAsync(staff.UserId, cancellationToken).ConfigureAwait(false);
        return user is null
            ? Result.Failure<StaffResponse>(GymErrors.NotFound)
            : Result.Success(new StaffResponse(staff.Id, user.Id, staff.EmployeeCode, user.Email, user.FirstName, user.LastName, staff.BranchId, staff.StaffType, staff.JoiningDate, staff.Status));
    }
}
