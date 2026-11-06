using FluentValidation;
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

public sealed record StaffCandidateMatch(Guid UserId, string Email, string FirstName, string LastName, string? Phone, bool AlreadyInWorkspace);

public sealed record LookupStaffCandidateQuery(string? Email, string? Phone) : IRequest<Result<StaffCandidateMatch?>>;

public sealed class LookupStaffCandidateQueryHandler : IRequestHandler<LookupStaffCandidateQuery, Result<StaffCandidateMatch?>>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _members;

    public LookupStaffCandidateQueryHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        ITenantMembershipRepository members)
    {
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _users = users;
        _members = members;
    }

    public async Task<Result<StaffCandidateMatch?>> Handle(LookupStaffCandidateQuery request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.UserCreate, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<StaffCandidateMatch?>(access.Error);
        }

        UserAccount? user = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        }

        if (user is null && !string.IsNullOrWhiteSpace(request.Phone))
        {
            user = await _users.GetByPhoneAsync(request.Phone.Trim(), cancellationToken).ConfigureAwait(false);
        }

        if (user is null)
        {
            return Result.Success<StaffCandidateMatch?>(null);
        }

        var member = await _members.GetActiveAsync(access.Value.TenantId, user.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success<StaffCandidateMatch?>(new StaffCandidateMatch(user.Id, user.Email, user.FirstName, user.LastName, user.Phone, member is not null));
    }
}

public static class StaffOnboardingRules
{
    public static StaffType StaffTypeFor(string roleName) => roleName switch
    {
        RoleNames.Trainer => StaffType.Trainer,
        RoleNames.Receptionist => StaffType.Receptionist,
        RoleNames.GymManager => StaffType.Manager,
        _ => StaffType.Other,
    };

    public static bool IsWizard(CreateWorkspaceUserCommand request) =>
        !string.IsNullOrWhiteSpace(request.Designation)
        || request.SaveAsDraft
        || request.SubscriptionId is not null
        || request.JoiningDate is not null
        || request.BranchId is not null;

    public static string NewEmployeeCode() => $"S{Guid.NewGuid():N}"[..9].ToUpperInvariant();
}

public sealed record CreateWorkspaceUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? EmployeeCode = null,
    string? Designation = null,
    string? Department = null,
    EmploymentType? EmploymentType = null,
    DateOnly? JoiningDate = null,
    Guid? BranchId = null,
    Guid? ReportingManagerUserId = null,
    string? EmploymentNotes = null,
    string? JobDescription = null,
    IReadOnlyList<Guid>? AuthorizedBranchIds = null,
    Guid? SubscriptionId = null,
    IReadOnlyList<AccessScopeInput>? ScopeOverrides = null,
    IReadOnlyList<string>? PermissionOverrides = null,
    string? Specialization = null,
    string? Certification = null,
    int? ExperienceYears = null,
    string? TrainingExpertise = null,
    bool LinkExistingUser = false,
    bool SaveAsDraft = false,
    bool SendInvitation = false) : IRequest<Result<WorkspaceUserCreated>>;

public sealed class CreateWorkspaceUserCommandValidator : AbstractValidator<CreateWorkspaceUserCommand>
{
    public CreateWorkspaceUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RoleName).Must(WorkspaceRoleRules.CanAssign).WithMessage("Choose a business role. SuperAdmin cannot be assigned.");
        RuleFor(command => command.Phone).NotEmpty().When(StaffOnboardingRules.IsWizard).WithMessage("Enter a mobile number.");
        RuleFor(command => command.Designation).NotEmpty().When(command => StaffOnboardingRules.IsWizard(command) && !command.SaveAsDraft).WithMessage("Enter a job title.");
        RuleFor(command => command.JoiningDate).NotEmpty().When(command => StaffOnboardingRules.IsWizard(command) && !command.SaveAsDraft);
        RuleFor(command => command.EmploymentType).IsInEnum().When(command => command.EmploymentType is not null);
    }
}
