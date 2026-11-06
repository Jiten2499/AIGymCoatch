using FluentValidation;
using LifeFitAI.Application.Email;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Staffing;

public sealed record AddCoachCommand(string Email, string FirstName, string LastName, string? Phone, string? Specialization, Guid? BranchId) : IRequest<Result<CoachCreated>>;

public sealed record CoachCreated(Guid Id, string Email, string FirstName, string LastName, string? Specialization, string? TemporaryPassword);

public sealed class AddCoachCommandValidator : AbstractValidator<AddCoachCommand>
{
    public AddCoachCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Phone).MaximumLength(32);
        RuleFor(command => command.Specialization).MaximumLength(200);
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
    private readonly IAccountActivationMailer? _activation;

    public AddCoachCommandHandler(IOperationGuard guard, ICurrentActor actor, IAccountLinker accounts, IUserAccountRepository users, ITenantMembershipRepository memberships, IRoleRepository roles, IPeopleRepository people, IBranchRepository branches, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client, ITenantRepository tenants, IAccountActivationMailer? activation = null)
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

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        var password = existing is null ? TemporaryPassword.Create() : null;
        var specialization = string.IsNullOrWhiteSpace(request.Specialization) ? null : request.Specialization.Trim();
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

                var role = await _roles.GetByNameAsync(RoleNames.Trainer, ct).ConfigureAwait(false) ?? throw new MemberOnboardingException(IdentityErrors.RoleNotFound);
                var names = await _roles.GetRoleNamesAsync(user.Id, tenantId, ct).ConfigureAwait(false);
                if (!names.Contains(RoleNames.Trainer))
                {
                    await _roles.AddAsync(UserRole.Create(Guid.NewGuid(), user.Id, role.Id, tenantId), ct).ConfigureAwait(false);
                }

                var today = DateOnly.FromDateTime(_clock.UtcNow);
                var staff = StaffProfile.Create(Guid.NewGuid(), tenantId, user.Id, $"S{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.BranchId, StaffType.Trainer, today);
                var trainer = TrainerProfile.Create(Guid.NewGuid(), tenantId, user.Id, request.BranchId);
                if (specialization is not null)
                {
                    trainer.Update(null, specialization, null, null, null, request.BranchId, ProfileStatus.Active);
                }

                await _people.AddStaffAsync(staff, ct).ConfigureAwait(false);
                await _people.AddTrainerAsync(trainer, ct).ConfigureAwait(false);
                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "AddCoach", "TrainerProfile", trainer.Id, null, user.Email, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                if (_activation is not null)
                {
                    var business = await _tenants.GetAsync(tenantId, ct).ConfigureAwait(false);
                    await _activation.SendAsync(new AccountActivationNotice(user.Id, user.Email, user.FirstName, user.LastName, RoleNames.Trainer, tenantId, business?.Name ?? "LifeFit AI", existing is not null), ct).ConfigureAwait(false);
                }

                response = new CoachCreated(trainer.Id, user.Email, user.FirstName, user.LastName, trainer.Specialization, null);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<CoachCreated>(exception.Error);
        }

        return Result.Success(response!);
    }
}
