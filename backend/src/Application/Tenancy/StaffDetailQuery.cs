using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record StaffDetailResponse(
    Guid UserId,
    Guid? StaffProfileId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string RoleName,
    bool IsOwner,
    string AccountStatus,
    string EmploymentStatus,
    string? EmployeeCode,
    string? Designation,
    string? Department,
    EmploymentType? EmploymentType,
    DateOnly? JoiningDate,
    Guid? BranchId,
    Guid? ReportingManagerUserId,
    string? EmploymentNotes,
    string? JobDescription,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    IReadOnlyList<Guid> AuthorizedBranchIds,
    Guid? SubscriptionId,
    string? PlanName,
    string? Specialization,
    string? Certification,
    int? ExperienceYears,
    string? TrainingExpertise,
    string MemberScope,
    string PaymentScope);

public sealed record GetStaffDetailQuery(Guid UserId) : IRequest<Result<StaffDetailResponse>>;

public sealed class GetStaffDetailQueryHandler : IRequestHandler<GetStaffDetailQuery, Result<StaffDetailResponse>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _members;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly ILicenseAdminStore _licenses;
    private readonly IStaffDetailReader _reader;

    public GetStaffDetailQueryHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IPeopleRepository people,
        ILicenseAdminStore licenses,
        IStaffDetailReader reader)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _members = members;
        _roles = roles;
        _people = people;
        _licenses = licenses;
        _reader = reader;
    }

    public async Task<Result<StaffDetailResponse>> Handle(GetStaffDetailQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserUpdate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<StaffDetailResponse>(access.Error);
        }

        if (await _members.GetActiveAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<StaffDetailResponse>(IdentityErrors.MemberNotFound);
        }

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<StaffDetailResponse>(IdentityErrors.MemberNotFound);
        }

        var names = await _roles.GetRoleNamesAsync(request.UserId, access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        var roleName = WorkspaceRoleRules.Display(names);
        var staff = await _people.GetStaffByUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        var trainer = await _people.GetTrainerByUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        var branches = staff is null
            ? Array.Empty<Guid>()
            : await _people.ListStaffBranchIdsAsync(access.Value.TenantId, staff.Id, cancellationToken).ConfigureAwait(false);
        var plans = await _licenses.ActiveUserPlansAsync(access.Value.TenantId, cancellationToken).ConfigureAwait(false);
        plans.TryGetValue(request.UserId, out var assignment);
        var seats = await _licenses.ActiveSeatsForUserAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        var seat = seats.FirstOrDefault(item => item.IsActive);
        var scopes = await _reader.UserScopesAsync(access.Value.TenantId, request.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new StaffDetailResponse(
            user.Id,
            staff?.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            roleName,
            request.UserId == access.Value.OwnerUserId,
            user.Status.ToString(),
            staff?.Status.ToString() ?? (user.Status == UserStatus.Disabled ? nameof(ProfileStatus.Inactive) : nameof(ProfileStatus.Active)),
            staff?.EmployeeCode,
            staff?.Designation,
            staff?.Department,
            staff?.EmploymentType,
            staff?.JoiningDate,
            staff?.BranchId,
            staff?.ReportingManagerUserId,
            staff?.EmploymentNotes,
            staff?.JobDescription,
            staff?.DateOfBirth,
            staff?.Gender,
            staff?.Address,
            staff?.EmergencyContactName,
            staff?.EmergencyContactPhone,
            branches,
            seat?.SubscriptionId,
            assignment?.PlanName,
            trainer?.Specialization,
            trainer?.Certification,
            trainer?.ExperienceYears,
            trainer?.Bio,
            scopes.GetValueOrDefault("members", AccessScopes.All),
            scopes.GetValueOrDefault("payments", AccessScopes.Own)));
    }
}

public interface IStaffDetailReader
{
    Task<IReadOnlyDictionary<string, string>> UserScopesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
