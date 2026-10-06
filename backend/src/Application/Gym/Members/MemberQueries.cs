using FluentValidation;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record SearchMembersQuery(string? Term, ProfileStatus? Status, Guid? BranchId, GymMembershipStatus? MembershipStatus, int Page, int PageSize, string? Sort) : IRequest<Result<PageResult<MemberResponse>>>;

public sealed class SearchMembersQueryValidator : AbstractValidator<SearchMembersQuery>
{
    public SearchMembersQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Sort).Must(sort => sort is null or "memberCode" or "name" or "joinDate");
    }
}

public sealed class SearchMembersQueryHandler : IRequestHandler<SearchMembersQuery, Result<PageResult<MemberResponse>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;

    public SearchMembersQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
    }

    public async Task<Result<PageResult<MemberResponse>>> Handle(SearchMembersQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<MemberResponse>>(gate.Error);
        }

        var page = await _people.SearchMembersAsync(tenantId, request.Term, request.Status, request.BranchId, request.MembershipStatus, new PageQuery(request.Page, request.PageSize, request.Sort), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }
}

public sealed record GetMemberQuery(Guid Id) : IRequest<Result<MemberResponse>>;

public sealed class GetMemberQueryHandler : IRequestHandler<GetMemberQuery, Result<MemberResponse>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IUserAccountRepository _users;

    public GetMemberQueryHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
    }

    public async Task<Result<MemberResponse>> Handle(GetMemberQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var user = profile is null ? null : await _users.GetByIdAsync(profile.UserId, cancellationToken).ConfigureAwait(false);
        return profile is null || user is null
            ? Result.Failure<MemberResponse>(GymErrors.NotFound)
            : Result.Success(MemberMaps.From(profile, user));
    }
}

public sealed record UpdateMemberCommand(Guid Id, DateOnly? DateOfBirth, string? Gender, string? EmergencyContactName, string? EmergencyContactPhone, string? Address, string? City, string? State, string? PostalCode, ProfileStatus Status) : IRequest<Result<MemberResponse>>;

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

    public UpdateMemberCommandHandler(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IUserAccountRepository users, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
    }

    public async Task<Result<MemberResponse>> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberUpdate, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MemberResponse>(gate.Error);
        }

        var profile = await _people.GetMemberAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        var user = profile is null ? null : await _users.GetByIdAsync(profile.UserId, cancellationToken).ConfigureAwait(false);
        if (profile is null || user is null)
        {
            return Result.Failure<MemberResponse>(GymErrors.NotFound);
        }

        profile.Update(request.DateOfBirth, request.Gender, request.EmergencyContactName, request.EmergencyContactPhone, request.Address, request.City, request.State, request.PostalCode, request.Status);
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, "UpdateMember", "MemberProfile", profile.Id, null, profile.MemberCode, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MemberMaps.From(profile, user));
    }
}
