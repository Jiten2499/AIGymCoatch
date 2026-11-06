using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record ListCommunicationsQuery(
    string? Term,
    CommunicationType? Type,
    CommunicationStatus? Status,
    int Page,
    int PageSize,
    CommunicationAudienceType? AudienceType = null,
    DateTime? PublishFrom = null,
    DateTime? PublishTo = null) : IRequest<Result<PageResult<CommunicationListItem>>>;

public sealed record GetCommunicationQuery(Guid Id) : IRequest<Result<CommunicationDetails>>;

public sealed record ListMemberCommunicationsQuery(CommunicationType? Type, int Page, int PageSize) : IRequest<Result<PageResult<MemberCommunicationItem>>>;

public sealed record GetMemberCommunicationQuery(Guid Id) : IRequest<Result<MemberCommunicationItem>>;

public sealed record MarkCommunicationReadCommand(Guid Id) : IRequest<Result>;

public sealed class CommunicationQueryHandler :
    IRequestHandler<ListCommunicationsQuery, Result<PageResult<CommunicationListItem>>>,
    IRequestHandler<GetCommunicationQuery, Result<CommunicationDetails>>,
    IRequestHandler<ListMemberCommunicationsQuery, Result<PageResult<MemberCommunicationItem>>>,
    IRequestHandler<GetMemberCommunicationQuery, Result<MemberCommunicationItem>>,
    IRequestHandler<MarkCommunicationReadCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ICommunicationRepository _communications;
    private readonly IPeopleRepository _people;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public CommunicationQueryHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ICommunicationRepository communications,
        IPeopleRepository people,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _communications = communications;
        _people = people;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<PageResult<CommunicationListItem>>> Handle(ListCommunicationsQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<CommunicationListItem>>(gate.Error);
        }

        var page = await _communications.SearchAsync(
            tenantId,
            new CommunicationSearch(request.Term, request.Type, request.Status, request.Page, request.PageSize, request.AudienceType, request.PublishFrom, request.PublishTo),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<CommunicationDetails>> Handle(GetCommunicationQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<CommunicationDetails>(gate.Error);
        }

        var details = await _communications.ReadAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return details is null
            ? Result.Failure<CommunicationDetails>(GymErrors.NotFound)
            : Result.Success(details);
    }

    public async Task<Result<PageResult<MemberCommunicationItem>>> Handle(ListMemberCommunicationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenMemberAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<MemberCommunicationItem>>(scope.Error);
        }

        var page = await _communications.ListForMemberAsync(
            scope.Value.TenantId,
            scope.Value.MemberId,
            scope.Value.UserId,
            request.Type,
            request.Page,
            request.PageSize,
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<MemberCommunicationItem>> Handle(GetMemberCommunicationQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenMemberAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<MemberCommunicationItem>(scope.Error);
        }

        var communication = await _communications.GetAsync(scope.Value.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication is null || !communication.IsVisibleToMembers(_clock.UtcNow))
        {
            return Result.Failure<MemberCommunicationItem>(GymErrors.NotFound);
        }

        if (!await _communications.IsAudienceMatchAsync(scope.Value.TenantId, communication, scope.Value.MemberId, _clock.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MemberCommunicationItem>(GymErrors.NotFound);
        }

        var isRead = await _communications.HasReadAsync(scope.Value.TenantId, communication.Id, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        var details = await _communications.ReadAsync(scope.Value.TenantId, communication.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new MemberCommunicationItem(
            communication.Id,
            communication.Type,
            communication.Title,
            communication.Message,
            communication.Priority,
            communication.PublishAt!.Value,
            communication.ExpiresAt,
            isRead,
            communication.ImageStorageKey,
            details?.CreatedBy));
    }

    public async Task<Result> Handle(MarkCommunicationReadCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenMemberAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var communication = await _communications.GetAsync(scope.Value.TenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (communication is null || !communication.IsVisibleToMembers(_clock.UtcNow))
        {
            return Result.Failure(GymErrors.NotFound);
        }

        if (!await _communications.IsAudienceMatchAsync(scope.Value.TenantId, communication, scope.Value.MemberId, _clock.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(GymErrors.NotFound);
        }

        if (!await _communications.HasReadAsync(scope.Value.TenantId, communication.Id, scope.Value.UserId, cancellationToken).ConfigureAwait(false))
        {
            await _communications.MarkReadAsync(
                CommunicationRead.Create(Guid.NewGuid(), scope.Value.TenantId, communication.Id, scope.Value.UserId, _clock.UtcNow),
                cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<Result<(Guid TenantId, Guid UserId, Guid MemberId)>> OpenMemberAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid, Guid)>(gate.Error);
        }

        var member = await _people.GetMemberByUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<(Guid, Guid, Guid)>(GymErrors.NotFound);
        }

        return Result.Success((tenantId, userId, member.Id));
    }
}
