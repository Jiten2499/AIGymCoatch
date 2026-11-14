using FluentValidation;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Licensing;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Staffing;

public sealed record AddCoachCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Specialization,
    Guid? BranchId,
    bool SendActivationLink = false,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? Designation = null,
    string? Bio = null,
    string? Certification = null,
    int? ExperienceYears = null,
    EmploymentType? EmploymentType = null,
    DateOnly? JoiningDate = null,
    Guid? ReportingManagerUserId = null,
    string? EmploymentNotes = null,
    ProfileStatus Status = ProfileStatus.Active,
    bool CommissionEligible = false,
    Guid? SubscriptionId = null) : IRequest<Result<CoachCreated>>;

public sealed record CoachCreated(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Specialization,
    string? TemporaryPassword,
    Guid? StaffProfileId = null);

public sealed class AddCoachCommandValidator : AbstractValidator<AddCoachCommand>
{
    public AddCoachCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Phone).NotEmpty().MaximumLength(32)
            .Must(phone => !string.IsNullOrWhiteSpace(phone) && MemberFieldRules.Phone(phone.Trim())).WithMessage("Enter a valid phone number.");
        RuleFor(command => command.Specialization).MaximumLength(200);
        RuleFor(command => command.Designation).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Bio).MaximumLength(2000);
        RuleFor(command => command.Certification).MaximumLength(500);
        RuleFor(command => command.Gender).MaximumLength(32);
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.EmergencyContactName).MaximumLength(200);
        RuleFor(command => command.EmergencyContactPhone).MaximumLength(32)
            .Must(phone => string.IsNullOrWhiteSpace(phone) || MemberFieldRules.Phone(phone.Trim()))
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactPhone));
        RuleFor(command => command.EmploymentNotes).MaximumLength(2000);
        RuleFor(command => command.ExperienceYears).InclusiveBetween(0, 80).When(command => command.ExperienceYears is not null);
        RuleFor(command => command.EmploymentType).IsInEnum().When(command => command.EmploymentType is not null);
        RuleFor(command => command.JoiningDate).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
    }
}

public sealed class UpdateTrainerCommandValidator : AbstractValidator<UpdateTrainerCommand>
{
    public UpdateTrainerCommandValidator()
    {
        RuleFor(command => command.Bio).MaximumLength(2000);
        RuleFor(command => command.Specialization).MaximumLength(200);
        RuleFor(command => command.Certification).MaximumLength(500);
        RuleFor(command => command.ProfileImageUrl).MaximumLength(500);
        RuleFor(command => command.ExperienceYears).InclusiveBetween(0, 80).When(command => command.ExperienceYears is not null);
        RuleFor(command => command.FirstName).MaximumLength(100);
        RuleFor(command => command.LastName).MaximumLength(100);
        RuleFor(command => command.Phone).MaximumLength(32);
        RuleFor(command => command.EmployeeCode).MaximumLength(32);
        RuleFor(command => command.Phone).Must(phone => string.IsNullOrWhiteSpace(phone) || MemberFieldRules.Phone(phone.Trim())).When(command => command.SetPhone);
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Designation).MaximumLength(120);
        RuleFor(command => command.Gender).MaximumLength(32);
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.EmergencyContactName).MaximumLength(200);
        RuleFor(command => command.EmergencyContactPhone).MaximumLength(32)
            .Must(phone => string.IsNullOrWhiteSpace(phone) || MemberFieldRules.Phone(phone.Trim()))
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactPhone));
        RuleFor(command => command.EmploymentNotes).MaximumLength(2000);
        RuleFor(command => command.EmploymentType).IsInEnum().When(command => command.EmploymentType is not null);
    }
}

public sealed class AddCoachCommandHandler : IRequestHandler<AddCoachCommand, Result<CoachCreated>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IAccountLinker _accounts;
    private readonly IUserAccountRepository _users;
    private readonly ITenantMembershipRepository _memberships;
    private readonly IRoleRepository _roles;
    private readonly IPeopleRepository _people;
    private readonly IBranchRepository _branches;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly ITenantRepository _tenants;
    private readonly ILicenseAdminStore _licenses;
    private readonly IPermissionCacheInvalidator _cache;
    private readonly IAccountActivationMailer? _activation;

    public AddCoachCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IAccountLinker accounts,
        IUserAccountRepository users,
        ITenantMembershipRepository memberships,
        IRoleRepository roles,
        IPeopleRepository people,
        IBranchRepository branches,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client,
        ITenantRepository tenants,
        ILicenseAdminStore licenses,
        IPermissionCacheInvalidator cache,
        IAccountActivationMailer? activation = null)
    {
        _guard = guard;
        _actor = actor;
        _accounts = accounts;
        _users = users;
        _memberships = memberships;
        _roles = roles;
        _people = people;
        _branches = branches;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _tenants = tenants;
        _licenses = licenses;
        _cache = cache;
        _activation = activation;
    }

    public async Task<Result<CoachCreated>> Handle(AddCoachCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.StaffCreate, FeatureCodes.StaffManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<CoachCreated>(gate.Error);
        }

        if (request.BranchId is Guid branchId)
        {
            var branch = await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false);
            if (branch is null || branch.Status != BranchStatus.Active)
            {
                return Result.Failure<CoachCreated>(GymErrors.NotFound);
            }
        }

        try
        {
            if (request.SubscriptionId is Guid seatPlanId)
            {
                await CoachSeatAssignment.ValidateCapacityAsync(_licenses, tenantId, seatPlanId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<CoachCreated>(exception.Error);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        var keepsPassword = await RehireCredential.KeepsPasswordAsync(existing, _memberships, cancellationToken).ConfigureAwait(false);
        var password = existing is null ? TemporaryPassword.Create() : null;
        var sendActivation = request.SendActivationLink || (existing is not null && !keepsPassword);
        var specialization = string.IsNullOrWhiteSpace(request.Specialization) ? null : request.Specialization.Trim();
        var joining = request.JoiningDate ?? DateOnly.FromDateTime(_clock.UtcNow);
        var designation = string.IsNullOrWhiteSpace(request.Designation) ? "Personal Trainer" : request.Designation.Trim();
        CoachCreated? response = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var linked = await _accounts.ResolveAsync(email, password, request.FirstName, request.LastName, request.Phone, ct).ConfigureAwait(false);
                if (linked.IsFailure)
                {
                    throw new MemberOnboardingException(linked.Error);
                }

                var user = linked.Value;
                if (await _people.GetStaffByUserAsync(tenantId, user.Id, ct).ConfigureAwait(false) is not null
                    || await _people.GetTrainerByUserAsync(tenantId, user.Id, ct).ConfigureAwait(false) is not null)
                {
                    throw new MemberOnboardingException(GymErrors.Conflict);
                }

                if (await _memberships.GetActiveAsync(tenantId, user.Id, ct).ConfigureAwait(false) is null)
                {
                    await _memberships.AddAsync(TenantMember.Create(Guid.NewGuid(), tenantId, user.Id, _clock.UtcNow), ct).ConfigureAwait(false);
                }

                var role = await _roles.GetByNameAsync(RoleNames.Trainer, ct).ConfigureAwait(false)
                    ?? throw new MemberOnboardingException(IdentityErrors.RoleNotFound);
                var names = await _roles.GetRoleNamesAsync(user.Id, tenantId, ct).ConfigureAwait(false);
                if (!names.Contains(RoleNames.Trainer))
                {
                    await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, tenantId), ct).ConfigureAwait(false);
                }

                var employeeCode = $"S{Guid.NewGuid():N}"[..9].ToUpperInvariant();
                // TrainerProfiles only allow Active/Inactive; keep new coaches Active so they appear in the default list.
                var profileStatus = request.Status == ProfileStatus.Inactive ? ProfileStatus.Inactive : ProfileStatus.Active;
                var staff = StaffProfile.Create(Guid.NewGuid(), tenantId, user.Id, employeeCode, request.BranchId, StaffType.Trainer, joining, profileStatus);
                staff.SetPersonal(request.DateOfBirth, request.Gender, request.Address, request.EmergencyContactName, request.EmergencyContactPhone);
                staff.SetEmployment(
                    designation,
                    null,
                    request.EmploymentType ?? EmploymentType.FullTime,
                    request.ReportingManagerUserId,
                    request.EmploymentNotes,
                    null,
                    joining,
                    request.BranchId);

                var trainer = TrainerProfile.Create(Guid.NewGuid(), tenantId, user.Id, request.BranchId);
                trainer.Update(
                    string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim(),
                    specialization,
                    request.ExperienceYears,
                    string.IsNullOrWhiteSpace(request.Certification) ? null : request.Certification.Trim(),
                    null,
                    request.BranchId,
                    profileStatus);
                trainer.SetEmployment(employeeCode, joining, request.CommissionEligible);

                await _people.AddStaffAsync(staff, ct).ConfigureAwait(false);
                await _people.AddTrainerAsync(trainer, ct).ConfigureAwait(false);
                if (request.SubscriptionId is Guid seatSubscriptionId && _actor.UserId is Guid actorId)
                {
                    await CoachSeatAssignment.AssignAsync(_licenses, _cache, tenantId, seatSubscriptionId, user.Id, actorId, _clock.UtcNow, ct).ConfigureAwait(false);
                }

                await _audit.AddAsync(
                    AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "AddCoach", "TrainerProfile", trainer.Id, null, user.Email, _client.IpAddress, _client.UserAgent, _clock.UtcNow),
                    ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                if (sendActivation && _activation is not null)
                {
                    var business = await _tenants.GetAsync(tenantId, ct).ConfigureAwait(false);
                    await _activation.SendAsync(
                        new AccountActivationNotice(user.Id, user.Email, user.FirstName, user.LastName, RoleNames.Trainer, tenantId, business?.Name ?? "LifeFit AI", keepsPassword),
                        ct).ConfigureAwait(false);
                }

                response = new CoachCreated(trainer.Id, user.Email, user.FirstName, user.LastName, trainer.Specialization, null, staff.Id);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<CoachCreated>(exception.Error);
        }

        return Result.Success(response!);
    }
}
