using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record MatchLeadMembersQuery(Guid LeadId, string? Mobile, string? Email) : IRequest<Result<IReadOnlyList<MemberMatchItem>>>;

public sealed class MatchLeadMembersQueryHandler : IRequestHandler<MatchLeadMembersQuery, Result<IReadOnlyList<MemberMatchItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ILeadRepository _leads;
    private readonly IPeopleRepository _people;

    public MatchLeadMembersQueryHandler(IOperationGuard guard, ICurrentActor actor, ILeadRepository leads, IPeopleRepository people)
    {
        _guard = guard;
        _actor = actor;
        _leads = leads;
        _people = people;
    }

    public async Task<Result<IReadOnlyList<MemberMatchItem>>> Handle(MatchLeadMembersQuery request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmConvert, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<MemberMatchItem>>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.LeadId, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return Result.Failure<IReadOnlyList<MemberMatchItem>>(GymErrors.NotFound);
        }

        var mobile = string.IsNullOrWhiteSpace(request.Mobile) ? lead.Mobile : request.Mobile.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? lead.Email : request.Email.Trim();
        var matches = await _people.FindMemberMatchesAsync(tenantId, mobile, email, cancellationToken).ConfigureAwait(false);
        return Result.Success(matches);
    }
}

public sealed record ConvertLeadCommand(
    Guid Id,
    Guid? MemberProfileId,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateOnly JoinDate,
    string? FitnessGoal,
    Guid? AssignedTrainerId,
    Guid? PlanId,
    Guid? BranchId,
    DateOnly? StartDate,
    bool AssignAppAccess = false,
    Guid? AppSubscriptionId = null,
    bool EnableBasicAppAccess = false) : IRequest<Result<LeadDetails>>;

public sealed class ConvertLeadCommandValidator : AbstractValidator<ConvertLeadCommand>
{
    public ConvertLeadCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100).When(command => command.MemberProfileId is null);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100).When(command => command.MemberProfileId is null);
        RuleFor(command => command.Phone).NotEmpty().Must(MemberFieldRules.Phone).When(command => command.MemberProfileId is null);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Email).NotEmpty().When(command => (command.AssignAppAccess || command.EnableBasicAppAccess) && command.MemberProfileId is null);
        RuleFor(command => command.Gender).Must(MemberFieldRules.Gender);
        RuleFor(command => command.EmergencyContactPhone).Must(MemberFieldRules.Phone);
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.FitnessGoal).Must(goal => MemberMaps.TryGoal(goal, out _));
        RuleFor(command => command.DateOfBirth).Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(command => command.JoinDate).Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow)).When(command => command.MemberProfileId is null);
        RuleFor(command => command.PlanId).NotEmpty().When(command => command.BranchId.HasValue);
        RuleFor(command => command.StartDate).NotNull().When(command => command.PlanId.HasValue);
        RuleFor(command => command.AppSubscriptionId).NotEmpty().When(command => command.AssignAppAccess);
    }
}

public sealed class ConvertLeadCommandHandler : IRequestHandler<ConvertLeadCommand, Result<LeadDetails>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPermissionService _permissions;
    private readonly ILeadRepository _leads;
    private readonly IPeopleRepository _people;
    private readonly IMembershipRepository _plans;
    private readonly IBranchRepository _branches;
    private readonly IMemberAppSeatAssigner _appSeats;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;

    public ConvertLeadCommandHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IPermissionService permissions,
        ILeadRepository leads,
        IPeopleRepository people,
        IMembershipRepository plans,
        IBranchRepository branches,
        IMemberAppSeatAssigner appSeats,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _permissions = permissions;
        _leads = leads;
        _people = people;
        _plans = plans;
        _branches = branches;
        _appSeats = appSeats;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<LeadDetails>> Handle(ConvertLeadCommand request, CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.CrmConvert, FeatureCodes.GymCrm, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<LeadDetails>(allowed.Error);
        }

        var lead = await _leads.GetAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.NotFound);
        }

        if (!LeadRules.CanConvert(lead.Status))
        {
            return Result.Failure<LeadDetails>(GymErrors.InvalidState);
        }

        MembershipPlan? plan = null;
        Branch? branch = null;
        if (request.PlanId is Guid planId)
        {
            var membershipGate = await _guard.AllowAsync(PermissionCodes.MembershipCreate, FeatureCodes.MembershipManagement, cancellationToken).ConfigureAwait(false);
            if (membershipGate.IsFailure)
            {
                return Result.Failure<LeadDetails>(membershipGate.Error);
            }

            plan = await _plans.GetPlanAsync(tenantId, planId, cancellationToken).ConfigureAwait(false);
            if (plan is null || !plan.IsActive)
            {
                return Result.Failure<LeadDetails>(plan is { IsActive: false } ? GymErrors.InvalidState : GymErrors.NotFound);
            }

            if (request.BranchId is Guid branchId)
            {
                branch = await _branches.GetAsync(tenantId, branchId, cancellationToken).ConfigureAwait(false);
                if (branch is null || branch.Status != BranchStatus.Active)
                {
                    return Result.Failure<LeadDetails>(GymErrors.NotFound);
                }
            }
        }

        if (request.AssignedTrainerId is Guid trainerId && request.MemberProfileId is null)
        {
            var trainer = await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false);
            if (trainer is null || trainer.Status != ProfileStatus.Active)
            {
                return Result.Failure<LeadDetails>(GymErrors.NotFound);
            }
        }

        if (!MemberMaps.TryGoal(request.FitnessGoal, out var goal))
        {
            return Result.Failure<LeadDetails>(GymErrors.InvalidState);
        }

        if ((request.EnableBasicAppAccess || request.AssignAppAccess) && string.IsNullOrWhiteSpace(request.Email) && request.MemberProfileId is null)
        {
            return Result.Failure<LeadDetails>(GymErrors.AppLoginRequired);
        }

        if (request.AssignAppAccess)
        {
            if (request.AppSubscriptionId is null)
            {
                return Result.Failure<LeadDetails>(GymErrors.AppSubscriptionRequired);
            }

            if (!await _permissions.HasPermissionAsync(userId, tenantId, PermissionCodes.LicenseManage, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<LeadDetails>(IdentityErrors.Forbidden);
            }
        }

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                MemberProfile member;
                if (request.MemberProfileId is Guid existingId)
                {
                    var existing = await _people.GetMemberAsync(tenantId, existingId, ct).ConfigureAwait(false);
                    if (existing is null)
                    {
                        throw new MemberOnboardingException(GymErrors.NotFound);
                    }

                    member = existing;
                }
                else
                {
                    var profile = MemberProfile.Create(Guid.NewGuid(), tenantId, null, $"M{Guid.NewGuid():N}"[..9].ToUpperInvariant(), request.JoinDate, request.FirstName.Trim(), request.LastName.Trim(), request.Phone);
                    profile.Update(request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.Address, null, null, null, ProfileStatus.Active, null, goal, request.AssignedTrainerId, request.JoinDate);
                    await _people.AddMemberAsync(profile, ct).ConfigureAwait(false);
                    await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "MemberCreated", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                    member = profile;
                }

                if (plan is not null)
                {
                    var start = request.StartDate ?? request.JoinDate;
                    var membership = GymMembership.Create(Guid.NewGuid(), tenantId, member.Id, branch?.Id, plan.Id, $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), start, plan.InclusiveEnd(start), DateOnly.FromDateTime(_clock.UtcNow), false, null);
                    membership.SetPricing(plan.Price, 0m, plan.Price, plan.Currency, null, null);
                    membership.ApplyIncludedSessions(plan.IncludedPtSessions);
                    if (await _plans.OverlapsMembershipAsync(tenantId, member.Id, membership.StartDate, membership.EndDate, null, ct).ConfigureAwait(false))
                    {
                        throw new MemberOnboardingException(GymErrors.Conflict);
                    }

                    await _plans.AddMembershipAsync(membership, ct).ConfigureAwait(false);
                    await _plans.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, MembershipAction.Created, null, membership.Status, start, "Lead conversion", userId, _clock.UtcNow), ct).ConfigureAwait(false);
                }

                if (request.EnableBasicAppAccess && member.UserId is null && !string.IsNullOrWhiteSpace(request.Email))
                {
                    var basic = await _appSeats.EnableBasicAccessAsync(tenantId, userId, member, request.Email, ct).ConfigureAwait(false);
                    if (basic.IsFailure)
                    {
                        throw new MemberOnboardingException(basic.Error);
                    }
                }

                if (request.AssignAppAccess && request.AppSubscriptionId is Guid appSubscriptionId)
                {
                    var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
                    var appPlan = await _appSeats.AssignAsync(tenantId, userId, member, appSubscriptionId, email, ct).ConfigureAwait(false);
                    if (appPlan.IsFailure)
                    {
                        throw new MemberOnboardingException(appPlan.Error);
                    }
                }

                if (!lead.Convert(member.Id, _clock.UtcNow, userId))
                {
                    throw new MemberOnboardingException(GymErrors.InvalidState);
                }

                if (!string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(lead.Email))
                {
                    lead.Update(lead.FullName, lead.Mobile, request.Email, lead.Source, lead.InterestedPlanId, lead.Notes);
                }

                await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, userId, "LeadConverted", "Lead", lead.Id, null, member.Id.ToString(), _client.IpAddress, _client.UserAgent, _clock.UtcNow), ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (MemberOnboardingException exception)
        {
            return Result.Failure<LeadDetails>(exception.Error);
        }

        return Result.Success((await _leads.ReadAsync(tenantId, lead.Id, cancellationToken).ConfigureAwait(false))!);
    }
}
