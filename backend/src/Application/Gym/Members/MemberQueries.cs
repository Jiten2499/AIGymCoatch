using FluentValidation;
using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record SearchMembersQuery(string? Term, ProfileStatus? Status, Guid? BranchId, GymMembershipStatus? MembershipStatus, int Page, int PageSize, string? Sort) : IRequest<Result<PageResult<MemberListItem>>>;

public sealed class SearchMembersQueryValidator : AbstractValidator<SearchMembersQuery>
{
    public SearchMembersQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Sort).Must(sort => sort is null or "memberCode" or "name" or "joinDate");
    }
}

public sealed class SearchMembersQueryHandler : IRequestHandler<SearchMembersQuery, Result<PageResult<MemberListItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IMemberAccessScope _scope;

    public SearchMembersQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IMemberAccessScope scope)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _scope = scope;
    }

    public async Task<Result<PageResult<MemberListItem>>> Handle(SearchMembersQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<MemberListItem>>(gate.Error);
        }

        var limit = await _scope.LimitAsync(tenantId, _actor.UserId!.Value, cancellationToken).ConfigureAwait(false);
        if (limit.Blocked)
        {
            return Result.Success(new PageResult<MemberListItem>([], request.Page, request.PageSize, 0));
        }

        var page = await _people.SearchMembersAsync(tenantId, request.Term, request.Status, request.BranchId, request.MembershipStatus, new PageQuery(request.Page, request.PageSize, request.Sort), cancellationToken, limit.OnlyUserId, limit.AssignedTrainerId).ConfigureAwait(false);
        return Result.Success(page);
    }
}

public sealed record GetMemberQuery(Guid Id) : IRequest<Result<MemberResponse>>;

public sealed class GetMemberQueryHandler : IRequestHandler<GetMemberQuery, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IMemberAccessScope _scope;

    public GetMemberQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IMemberAccessScope scope)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _scope = scope;
    }

    public async Task<Result<MemberResponse>> Handle(GetMemberQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var limit = await _scope.LimitAsync(tenantId, _actor.UserId!.Value, cancellationToken).ConfigureAwait(false);
        var allowed = profile is not null && !limit.Blocked
            && (limit.Scope == Domain.Identity.AccessScopes.All
                || (limit.OnlyUserId is Guid userId && profile.UserId == userId)
                || (limit.AssignedTrainerId is Guid trainerId && profile.AssignedTrainerId == trainerId));
        if (!allowed)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        var member = await _people.ReadMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(member);
    }
}

public sealed record UpdateMemberCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    DateOnly? JoinDate,
    string? FitnessGoal,
    Guid? AssignedTrainerId,
    ProfileStatus Status) : IRequest<Result<MemberResponse>>;

public sealed class UpdateMemberCommandValidator : AbstractValidator<UpdateMemberCommand>
{
    public UpdateMemberCommandValidator()
    {
        RuleFor(command => command.FirstName).MaximumLength(100);
        RuleFor(command => command.LastName).MaximumLength(100);
        RuleFor(command => command.Phone).Must(MemberFieldRules.Phone);
        RuleFor(command => command.Gender).Must(MemberFieldRules.Gender);
        RuleFor(command => command.EmergencyContactName).MaximumLength(200);
        RuleFor(command => command.EmergencyContactPhone).Must(MemberFieldRules.Phone);
        RuleFor(command => command.EmergencyContactRelationship).MaximumLength(64);
        RuleFor(command => command.Address).MaximumLength(300);
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.State).MaximumLength(100);
        RuleFor(command => command.PostalCode).MaximumLength(20);
        RuleFor(command => command.FitnessGoal).Must(goal => MemberMaps.TryGoal(goal, out _));
        RuleFor(command => command.DateOfBirth).Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(command => command.JoinDate).Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(command => command.Status).IsInEnum();
    }
}

public sealed class UpdateMemberCommandHandler : IRequestHandler<UpdateMemberCommand, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly Trainers.ITrainerWorkspace? _trainers;

    public UpdateMemberCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client, Trainers.ITrainerWorkspace? trainers = null)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _trainers = trainers;
    }

    public async Task<Result<MemberResponse>> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid actorId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var user = profile?.UserId is Guid linkedUserId ? await _users.GetByIdAsync(linkedUserId, cancellationToken).ConfigureAwait(false) : null;
        if (profile is null || (profile.UserId is not null && user is null) || !MemberMaps.TryGoal(request.FitnessGoal, out var goal))
        {
            return Result.Failure<MemberResponse>(profile is null || (profile.UserId is not null && user is null) ? GymErrors.NotFound : GymErrors.InvalidState);
        }

        if (request.AssignedTrainerId is Guid trainerId)
        {
            var trainer = await _people.GetTrainerAsync(tenantId, trainerId, cancellationToken).ConfigureAwait(false);
            if (trainer is null || trainer.Status != ProfileStatus.Active)
            {
                return Result.Failure<MemberResponse>(GymErrors.NotFound);
            }
        }

        if (request.Phone is not null && user is not null)
        {
            var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            if (phone is not null && !string.Equals(phone, user.Phone, StringComparison.Ordinal) && await _users.PhoneExistsAsync(phone, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<MemberResponse>(IdentityErrors.PhoneConflict);
            }

            user.ChangePhone(phone);
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName))
        {
            profile.SetContact(request.FirstName, request.LastName, request.Phone ?? profile.Phone);
            user?.Rename(request.FirstName, request.LastName);
        }
        var previous = profile.Status;
        profile.Update(request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.Address, request.City, request.State, request.PostalCode, request.Status, request.EmergencyContactRelationship, goal, request.AssignedTrainerId, request.JoinDate);
        if (_trainers is not null)
        {
            await _trainers.SyncMemberTrainerAsync(tenantId, profile.Id, request.AssignedTrainerId, request.JoinDate ?? DateOnly.FromDateTime(_clock.UtcNow), actorId, cancellationToken).ConfigureAwait(false);
        }
        var now = _clock.UtcNow;
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberUpdated", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        if (previous != profile.Status)
        {
            await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MemberStatusChanged", "MemberProfile", profile.Id, previous.ToString(), profile.Status.ToString(), _client.IpAddress, _client.UserAgent, now), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var member = await _people.ReadMemberAsync(tenantId, profile.Id, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<MemberResponse>(GymErrors.NotFound) : Result.Success(member);
    }
}
