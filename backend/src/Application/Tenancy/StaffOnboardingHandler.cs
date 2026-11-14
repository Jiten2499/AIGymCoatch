using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed class StaffOnboardingHandler : IRequestHandler<CreateWorkspaceUserCommand, Result<WorkspaceUserCreated>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly IAccountLinker _accounts;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly ILicenseAdminStore _licenses;
    private readonly IAccessRoleStore _accessRoles;
    private readonly IAccountActivationMailer? _activation;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public StaffOnboardingHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        IAccountLinker accounts,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IPeopleRepository people,
        IBranchRepository branches,
        ILicenseAdminStore licenses,
        IAccessRoleStore accessRoles,
        IPermissionCacheInvalidator cache,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        IAccountActivationMailer? activation = null)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _accounts = accounts;
        _members = members;
        _roles = roles;
        _people = people;
        _branches = branches;
        _licenses = licenses;
        _accessRoles = accessRoles;
        _activation = activation;
        _cache = cache;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<WorkspaceUserCreated>> Handle(CreateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserCreate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<WorkspaceUserCreated>(access.Error);
        }

        if (request.RoleName == RoleNames.SuperAdmin || !WorkspaceRoleRules.CanAssign(request.RoleName))
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.SuperAdminRejected);
        }

        var actorRoles = await _roles.GetRoleNamesAsync(access.Value.ActorId, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AccessRoleRules.OwnerAssignmentAllowed(request.RoleName, actorRoles))
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.OwnerAssignmentRejected);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null && await _members.GetActiveAsync(access.Value.TenantId, existing.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.MembershipConflict);
        }

        if (existing is not null && StaffOnboardingRules.IsWizard(request) && !request.LinkExistingUser && !request.SaveAsDraft)
        {
            return Result.Failure<WorkspaceUserCreated>(new Error("staff.existing_user", "That email already belongs to a LifeFit AI user. Confirm linking the existing account."));
        }

        var role = await _roles.GetAssignableAsync(access.Value.TenantId, request.RoleName, cancellationToken).ConfigureAwait(false);
        if (role is null || !WorkspaceRoleRules.IsAssignable(role, access.Value.TenantId))
        {
            return Result.Failure<WorkspaceUserCreated>(IdentityErrors.RoleNotFound);
        }

        if (request.BranchId is Guid branchId)
        {
            var branch = await _branches.GetAsync(access.Value.TenantId, branchId, cancellationToken).ConfigureAwait(false);
            if (branch is null || branch.Status != BranchStatus.Active)
            {
                return Result.Failure<WorkspaceUserCreated>(GymErrors.NotFound);
            }
        }

        var wizard = StaffOnboardingRules.IsWizard(request);
        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? StaffOnboardingRules.NewEmployeeCode() : request.EmployeeCode.Trim().ToUpperInvariant();
        if (wizard && await _people.EmployeeCodeExistsAsync(access.Value.TenantId, employeeCode, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<WorkspaceUserCreated>(GymErrors.Conflict);
        }

        if (request.SubscriptionId is Guid subscriptionId && !request.SaveAsDraft)
        {
            var subscription = await _licenses.GetTenantSubscriptionAsync(access.Value.TenantId, subscriptionId, cancellationToken).ConfigureAwait(false);
            if (subscription is null || !subscription.IsUsable(_clock.UtcNow))
            {
                return Result.Failure<WorkspaceUserCreated>(new Error("license.not_found", "That plan is not assigned to this workspace."));
            }

            var used = await _licenses.ActiveSeatCountAsync(subscription.Id, cancellationToken).ConfigureAwait(false);
            if (!SeatAllocator.HasCapacity(subscription.Seats, used))
            {
                return Result.Failure<WorkspaceUserCreated>(new Error("license.capacity_conflict", "No seats are available on that plan."));
            }
        }

        var invite = wizard && request.SendInvitation && !request.SaveAsDraft && existing is null;
        var keepsPassword = await RehireCredential.KeepsPasswordAsync(existing, _members, cancellationToken).ConfigureAwait(false);
        var password = existing is null ? TemporaryPassword.Create() : null;
        WorkspaceUserCreated? created = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var linked = await _accounts.ResolveAsync(email, password, request.FirstName, request.LastName, request.Phone, ct).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    throw new WorkspaceUserException(linked.Error);
                }

                var user = linked.Value;
                if (!string.IsNullOrWhiteSpace(request.Phone) && user.Phone is null)
                {
                    user.ChangePhone(request.Phone);
                }

                var now = _clock.UtcNow;
                if (await _members.GetActiveAsync(access.Value.TenantId, user.Id, ct).ConfigureAwait(false) is null)
                {
                    await _members.AddAsync(TenantMember.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, now), ct).ConfigureAwait(false);
                }

                var names = await _roles.GetRoleNamesAsync(user.Id, access.Value.TenantId, ct).ConfigureAwait(false);
                if (!names.Contains(request.RoleName, StringComparer.Ordinal))
                {
                    await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, access.Value.TenantId), ct).ConfigureAwait(false);
                }

                Guid? staffProfileId = null;
                string status = "Active";
                if (wizard)
                {
                    var profileStatus = request.SaveAsDraft
                        ? ProfileStatus.Draft
                        : invite ? ProfileStatus.PendingInvitation : ProfileStatus.Active;
                    status = profileStatus.ToString();
                    var staffType = StaffOnboardingRules.StaffTypeFor(request.RoleName);
                    var joining = request.JoiningDate ?? DateOnly.FromDateTime(now);
                    var staff = StaffProfile.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, employeeCode, request.BranchId, staffType, joining, profileStatus);
                    staff.SetPersonal(request.DateOfBirth, request.Gender, request.Address, request.EmergencyContactName, request.EmergencyContactPhone);
                    staff.SetEmployment(request.Designation ?? request.RoleName, request.Department, request.EmploymentType, request.ReportingManagerUserId, request.EmploymentNotes, request.JobDescription, joining, request.BranchId);
                    await _people.AddStaffAsync(staff, ct).ConfigureAwait(false);
                    staffProfileId = staff.Id;
                    foreach (var authorized in (request.AuthorizedBranchIds ?? []).Distinct())
                    {
                        await _people.AddStaffBranchAccessAsync(StaffBranchAccess.Create(Guid.NewGuid(), access.Value.TenantId, staff.Id, authorized), ct).ConfigureAwait(false);
                    }

                    if (staffType == StaffType.Trainer || request.RoleName == RoleNames.Trainer)
                    {
                        var trainer = await _people.GetTrainerByUserAsync(access.Value.TenantId, user.Id, ct).ConfigureAwait(false);
                        if (trainer is null)
                        {
                            trainer = TrainerProfile.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, request.BranchId);
                            trainer.Update(request.TrainingExpertise, request.Specialization, request.ExperienceYears, request.Certification, null, request.BranchId, ProfileStatus.Active);
                            trainer.SetEmployment(employeeCode, joining, false);
                            await _people.AddTrainerAsync(trainer, ct).ConfigureAwait(false);
                        }
                    }
                }

                if (request.SubscriptionId is Guid seatSubscriptionId && !request.SaveAsDraft)
                {
                    var reservation = await _licenses.ReserveSeatAsync(access.Value.TenantId, seatSubscriptionId, user.Id, access.Value.ActorId, now, ct).ConfigureAwait(false);
                    if (!reservation.Succeeded || reservation.Seat is null)
                    {
                        throw new WorkspaceUserException(new Error("license.capacity_conflict", "No seats are available on that plan."));
                    }

                    _cache.Schedule(access.Value.TenantId, user.Id);
                    _cache.ScheduleFeatures(access.Value.TenantId);
                }

                if (await _permissions.HasPermissionAsync(access.Value.ActorId, access.Value.TenantId, PermissionCodes.RoleAssign, ct).ConfigureAwait(false))
                {
                    await ApplyOverridesAsync(access.Value.TenantId, user.Id, request, ct).ConfigureAwait(false);
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "CreateWorkspaceUser", "TenantMember", user.Id, null, request.RoleName, _client.IpAddress, _client.UserAgent, now), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

                created = new WorkspaceUserCreated(
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    request.RoleName,
                    null,
                    staffProfileId,
                    null,
                    status,
                    wizard ? employeeCode : null);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceUserException exception)
        {
            return Result.Failure<WorkspaceUserCreated>(exception.Error);
        }

        if (created is not null && request.SendInvitation && !request.SaveAsDraft && _activation is not null)
        {
            var business = await _tenants.GetAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
            await _activation.SendAsync(
                new AccountActivationNotice(created.Id, created.Email, created.FirstName, created.LastName, request.RoleName, access.Value.TenantId, business?.Name ?? "LifeFit AI", keepsPassword),
                cancellationToken).ConfigureAwait(false);
        }

        return created is null
            ? Result.Failure<WorkspaceUserCreated>(IdentityErrors.RoleNotFound)
            : Result.Success(created);
    }

    private async Task ApplyOverridesAsync(Guid tenantId, Guid userId, CreateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        var codes = (request.PermissionOverrides ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (codes.Length > 0)
        {
            var ids = await _accessRoles.PermissionIdsAsync(codes, cancellationToken).ConfigureAwait(false);
            foreach (var code in codes)
            {
                if (!ids.TryGetValue(code, out var permissionId))
                {
                    continue;
                }

                await _accessRoles.AddUserPermissionOverrideAsync(tenantId, userId, permissionId, true, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var scope in request.ScopeOverrides ?? [])
        {
            if (!AccessScopes.IsKnown(scope.Scope))
            {
                continue;
            }

            await _accessRoles.AddUserResourceScopeAsync(tenantId, userId, scope.Resource, scope.Scope, cancellationToken).ConfigureAwait(false);
        }
    }
}
