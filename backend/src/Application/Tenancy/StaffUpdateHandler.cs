using LifeFitAI.Application.Authorization;
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

public sealed class StaffUpdateHandler : IRequestHandler<UpdateWorkspaceUserCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly ILicenseAdminStore _licenses;
    private readonly IAccessRoleStore _accessRoles;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public StaffUpdateHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
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
        IClientContext client)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _members = members;
        _roles = roles;
        _people = people;
        _branches = branches;
        _licenses = licenses;
        _accessRoles = accessRoles;
        _cache = cache;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(UpdateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserUpdate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        if (request.RoleName == RoleNames.SuperAdmin || !WorkspaceRoleRules.CanAssign(request.RoleName))
        {
            return Result.Failure(IdentityErrors.SuperAdminRejected);
        }

        var actorRoles = await _roles.GetRoleNamesAsync(access.Value.ActorId, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AccessRoleRules.OwnerAssignmentAllowed(request.RoleName, actorRoles))
        {
            return Result.Failure(IdentityErrors.OwnerAssignmentRejected);
        }

        if (await _members.GetActiveAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.MemberNotFound);
        }

        var owner = request.UserId == access.Value.OwnerUserId;
        if (owner && request.RoleName != RoleNames.GymOwner)
        {
            return Result.Failure(IdentityErrors.OwnerRoleRejected);
        }

        if (request.Status == UserStatus.Disabled && user.Status != UserStatus.Disabled)
        {
            if (request.UserId == access.Value.ActorId)
            {
                return Result.Failure(IdentityErrors.SelfDisableRejected);
            }

            if (owner)
            {
                return Result.Failure(IdentityErrors.OwnerDisableRejected);
            }
        }

        if (request.BranchId is Guid branchId)
        {
            var branch = await _branches.GetAsync(access.Value.TenantId, branchId, cancellationToken).ConfigureAwait(false);
            if (branch is null)
            {
                return Result.Failure(GymErrors.NotFound);
            }
        }

        var staff = await _people.GetStaffByUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.EmployeeCode) && staff is not null)
        {
            if (await _people.EmployeeCodeExistsAsync(access.Value.TenantId, request.EmployeeCode.Trim().ToUpperInvariant(), cancellationToken, staff.Id).ConfigureAwait(false))
            {
                return Result.Failure(GymErrors.Conflict);
            }
        }

        var names = await _roles.GetRoleNamesAsync(request.UserId, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        if (!names.Contains(request.RoleName, StringComparer.Ordinal))
        {
            if (!await _permissions.HasPermissionAsync(access.Value.ActorId, access.Value.TenantId, PermissionCodes.RoleAssign, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(IdentityErrors.Forbidden);
            }

            var role = await _roles.GetAssignableAsync(access.Value.TenantId, request.RoleName, cancellationToken).ConfigureAwait(false);
            if (role is null || !WorkspaceRoleRules.IsAssignable(role, access.Value.TenantId))
            {
                return Result.Failure(IdentityErrors.RoleNotFound);
            }

            var nowRole = _clock.UtcNow;
            await _roles.RemoveForMemberAsync(request.UserId, access.Value.TenantId, nowRole, cancellationToken).ConfigureAwait(false);
            await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), request.UserId, role.Id, access.Value.TenantId), cancellationToken).ConfigureAwait(false);
        }

        user.Rename(request.FirstName, request.LastName);
        if (request.Phone is not null)
        {
            user.ChangePhone(request.Phone);
        }

        var profileStatus = ResolveProfileStatus(request.EmploymentStatus, request.Status);
        if (profileStatus == ProfileStatus.Inactive || request.Status == UserStatus.Disabled)
        {
            user.Disable();
        }
        else
        {
            user.Activate();
        }

        var now = _clock.UtcNow;
        var staffType = StaffOnboardingRules.StaffTypeFor(request.RoleName);
        if (staff is null && !string.IsNullOrWhiteSpace(request.Designation))
        {
            var code = string.IsNullOrWhiteSpace(request.EmployeeCode) ? StaffOnboardingRules.NewEmployeeCode() : request.EmployeeCode.Trim().ToUpperInvariant();
            staff = StaffProfile.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, code, request.BranchId, staffType, request.JoiningDate ?? DateOnly.FromDateTime(now), profileStatus);
            await _people.AddStaffAsync(staff, cancellationToken).ConfigureAwait(false);
        }

        if (staff is not null)
        {
            staff.Update(request.BranchId ?? staff.BranchId, staffType, profileStatus);
            staff.SetPersonal(request.DateOfBirth, request.Gender, request.Address, request.EmergencyContactName, request.EmergencyContactPhone);
            if (!string.IsNullOrWhiteSpace(request.Designation))
            {
                staff.SetEmployment(
                    request.Designation,
                    request.Department,
                    request.EmploymentType,
                    request.ReportingManagerUserId,
                    request.EmploymentNotes,
                    request.JobDescription,
                    request.JoiningDate ?? staff.JoiningDate,
                    request.BranchId ?? staff.BranchId);
            }

            if (!string.IsNullOrWhiteSpace(request.EmployeeCode))
            {
                staff.SetEmployeeCode(request.EmployeeCode);
            }

            var authorized = request.AuthorizedBranchIds ?? (request.BranchId is Guid primary ? [primary] : Array.Empty<Guid>());
            await _people.ReplaceStaffBranchAccessAsync(access.Value.TenantId, staff.Id, authorized, now, cancellationToken).ConfigureAwait(false);

            if (staffType == StaffType.Trainer || request.RoleName == RoleNames.Trainer)
            {
                var trainer = await _people.GetTrainerByUserAsync(access.Value.TenantId, user.Id, cancellationToken).ConfigureAwait(false);
                if (trainer is null)
                {
                    trainer = TrainerProfile.Create(Guid.NewGuid(), access.Value.TenantId, user.Id, request.BranchId ?? staff.BranchId);
                    await _people.AddTrainerAsync(trainer, cancellationToken).ConfigureAwait(false);
                }

                trainer.Update(request.TrainingExpertise, request.Specialization, request.ExperienceYears, request.Certification, null, request.BranchId ?? staff.BranchId, ProfileStatus.Active);
                trainer.SetEmployment(staff.EmployeeCode, request.JoiningDate ?? staff.JoiningDate, trainer.CommissionEligible);
            }
        }

        if (request.SubscriptionId is Guid subscriptionId)
        {
            var current = await _licenses.ActiveSeatsForUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (!current.Any(seat => seat.IsActive && seat.SubscriptionId == subscriptionId))
            {
                var subscription = await _licenses.GetTenantSubscriptionAsync(access.Value.TenantId, subscriptionId, cancellationToken).ConfigureAwait(false);
                if (subscription is null || !subscription.IsUsable(now))
                {
                    return Result.Failure(new Error("license.not_found", "That plan is not assigned to this workspace."));
                }

                foreach (var seat in current.Where(seat => seat.IsActive))
                {
                    seat.Release(now, access.Value.ActorId);
                }

                var reservation = await _licenses.ReserveSeatAsync(access.Value.TenantId, subscriptionId, request.UserId, access.Value.ActorId, now, cancellationToken).ConfigureAwait(false);
                if (!reservation.Succeeded)
                {
                    return Result.Failure(reservation.Error ?? new Error("license.capacity_conflict", "No seats are available on that plan."));
                }

                _cache.Schedule(access.Value.TenantId, request.UserId);
                _cache.ScheduleFeatures(access.Value.TenantId);
            }
        }

        if (await _permissions.HasPermissionAsync(access.Value.ActorId, access.Value.TenantId, PermissionCodes.RoleAssign, cancellationToken).ConfigureAwait(false))
        {
            await ApplyOverridesAsync(access.Value.TenantId, request.UserId, request, cancellationToken).ConfigureAwait(false);
        }

        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), access.Value.TenantId, access.Value.ActorId, "UpdateWorkspaceUser", "User", user.Id, null, request.RoleName, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task ApplyOverridesAsync(Guid tenantId, Guid userId, UpdateWorkspaceUserCommand request, CancellationToken cancellationToken)
    {
        foreach (var code in (request.PermissionOverrides ?? []).Distinct(StringComparer.Ordinal))
        {
            var ids = await _accessRoles.PermissionIdsAsync([code], cancellationToken).ConfigureAwait(false);
            if (ids.TryGetValue(code, out var permissionId))
            {
                await _accessRoles.AddUserPermissionOverrideAsync(tenantId, userId, permissionId, true, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var scope in request.ScopeOverrides ?? [])
        {
            if (AccessScopes.IsKnown(scope.Scope))
            {
                await _accessRoles.AddUserResourceScopeAsync(tenantId, userId, scope.Resource, scope.Scope, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static ProfileStatus ResolveProfileStatus(string? employmentStatus, UserStatus accountStatus)
    {
        if (accountStatus == UserStatus.Disabled)
        {
            return ProfileStatus.Inactive;
        }

        return employmentStatus switch
        {
            nameof(ProfileStatus.Draft) => ProfileStatus.Draft,
            nameof(ProfileStatus.PendingInvitation) => ProfileStatus.PendingInvitation,
            nameof(ProfileStatus.Suspended) => ProfileStatus.Suspended,
            "Deactivated" or nameof(ProfileStatus.Inactive) => ProfileStatus.Inactive,
            _ => ProfileStatus.Active,
        };
    }
}
