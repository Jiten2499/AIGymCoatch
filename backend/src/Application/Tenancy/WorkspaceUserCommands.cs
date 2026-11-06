using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Tenancy;

public sealed record UpdateWorkspaceUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    UserStatus Status,
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
    LifeFitAI.Domain.Gym.EmploymentType? EmploymentType = null,
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
    string? EmploymentStatus = null) : IRequest<Result>;

public sealed class UpdateWorkspaceUserCommandValidator : AbstractValidator<UpdateWorkspaceUserCommand>
{
    public UpdateWorkspaceUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.RoleName).Must(WorkspaceRoleRules.CanAssign).WithMessage("Choose a business role. SuperAdmin cannot be assigned.");
        RuleFor(command => command.EmploymentType).IsInEnum().When(command => command.EmploymentType is not null);
        RuleFor(command => command.Phone).NotEmpty().When(command => !string.IsNullOrWhiteSpace(command.Designation));
        RuleFor(command => command.Designation).NotEmpty().When(command => command.JoiningDate is not null || command.BranchId is not null);
    }
}

public sealed record DeleteWorkspaceUsersCommand(IReadOnlyList<Guid> UserIds) : IRequest<Result>;

public sealed class DeleteWorkspaceUsersCommandValidator : AbstractValidator<DeleteWorkspaceUsersCommand>
{
    public DeleteWorkspaceUsersCommandValidator() => RuleFor(command => command.UserIds).NotEmpty();
}

public sealed class WorkspaceUserCommandHandler : IRequestHandler<DeleteWorkspaceUsersCommand, Result>
{
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ITenantRepository _tenants;
    private readonly ITenantMembershipRepository _members;
    private readonly IBusinessProfileSoftDelete _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public WorkspaceUserCommandHandler(
        ICurrentActor actor,
        IPermissionService permissions,
        ITenantRepository tenants,
        IUserAccountRepository users,
        IAccountLinker accounts,
        ITenantMembershipRepository members,
        IRoleRepository roles,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        IBusinessProfileSoftDelete profiles)
    {
        _ = users;
        _ = accounts;
        _ = roles;
        _ = audit;
        _actor = actor;
        _permissions = permissions;
        _tenants = tenants;
        _members = members;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result> Handle(DeleteWorkspaceUsersCommand request, CancellationToken cancellationToken)
    {
        var access = await WorkspaceAccess.OpenAsync(_actor, _tenants, _permissions, PermissionCodes.TenantMemberRemove, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        var ids = request.UserIds.Distinct().ToArray();
        foreach (var userId in ids)
        {
            if (userId == access.Value.OwnerUserId)
            {
                return Result.Failure(IdentityErrors.OwnerRemovalRejected);
            }

            if (userId == access.Value.ActorId)
            {
                return Result.Failure(IdentityErrors.SelfRemovalRejected);
            }

            if (await _members.GetActiveAsync(access.Value.TenantId, userId, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(IdentityErrors.MemberNotFound);
            }
        }

        return await BusinessProfileDeletion.Execute(
            _unitOfWork,
            token => _profiles.DeleteStaffUsersAsync(access.Value.TenantId, access.Value.ActorId, ids, _clock.UtcNow, _client.IpAddress, _client.UserAgent, token),
            cancellationToken).ConfigureAwait(false);
    }
}

sealed class WorkspaceUserException : Exception
{
    public WorkspaceUserException(Error error) => Error = error;

    public Error Error { get; }
}
