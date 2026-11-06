using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record PreviewCommunicationAudienceQuery(
    CommunicationAudienceType AudienceType,
    Guid? AudiencePackageId,
    Guid? AudienceTrainerId,
    int? ExpiringWithinDays,
    IReadOnlyList<Guid>? SelectedMemberIds,
    IReadOnlyList<Guid>? SelectedTrainerIds,
    IReadOnlyList<Guid>? SelectedStaffIds) : IRequest<Result<AudiencePreview>>;

public sealed record ListInboxCommunicationsQuery(CommunicationType? Type, bool UnreadOnly, int Page, int PageSize) : IRequest<Result<PageResult<MemberCommunicationItem>>>;

public sealed record GetInboxCommunicationQuery(Guid Id) : IRequest<Result<MemberCommunicationItem>>;

public sealed record CountUnreadCommunicationsQuery : IRequest<Result<int>>;

public sealed record GetCommunicationInboxSummaryQuery : IRequest<Result<CommunicationInboxSummaryResponse>>;

public sealed record SyncInboxCommunicationsQuery(DateTime? SinceUtc, int Take) : IRequest<Result<CommunicationInboxSyncResponse>>;

public sealed record MarkInboxCommunicationReadCommand(Guid Id) : IRequest<Result>;

public sealed record MarkAllInboxCommunicationsReadCommand : IRequest<Result>;

public sealed class CommunicationInboxHandler :
    IRequestHandler<PreviewCommunicationAudienceQuery, Result<AudiencePreview>>,
    IRequestHandler<ListInboxCommunicationsQuery, Result<PageResult<MemberCommunicationItem>>>,
    IRequestHandler<GetInboxCommunicationQuery, Result<MemberCommunicationItem>>,
    IRequestHandler<CountUnreadCommunicationsQuery, Result<int>>,
    IRequestHandler<GetCommunicationInboxSummaryQuery, Result<CommunicationInboxSummaryResponse>>,
    IRequestHandler<SyncInboxCommunicationsQuery, Result<CommunicationInboxSyncResponse>>,
    IRequestHandler<MarkInboxCommunicationReadCommand, Result>,
    IRequestHandler<MarkAllInboxCommunicationsReadCommand, Result>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly ICommunicationRepository _communications;
    private readonly ICommunicationDelivery _delivery;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public CommunicationInboxHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        ICommunicationRepository communications,
        ICommunicationDelivery delivery,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _guard = guard;
        _actor = actor;
        _communications = communications;
        _delivery = delivery;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AudiencePreview>> Handle(PreviewCommunicationAudienceQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationCreate, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<AudiencePreview>(gate.Error);
        }

        var preview = await _delivery.PreviewAsync(
            tenantId,
            request.AudienceType,
            request.AudiencePackageId,
            request.AudienceTrainerId,
            request.ExpiringWithinDays,
            request.SelectedMemberIds ?? [],
            request.SelectedTrainerIds ?? [],
            request.SelectedStaffIds ?? [],
            _clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(preview);
    }

    public async Task<Result<PageResult<MemberCommunicationItem>>> Handle(ListInboxCommunicationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<PageResult<MemberCommunicationItem>>(scope.Error);
        }

        return Result.Success(await _communications.ListInboxAsync(scope.Value.TenantId, scope.Value.UserId, request.Type, request.UnreadOnly, request.Page, request.PageSize, _clock.UtcNow, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<MemberCommunicationItem>> Handle(GetInboxCommunicationQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<MemberCommunicationItem>(scope.Error);
        }

        var item = await _communications.ReadInboxAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return item is null ? Result.Failure<MemberCommunicationItem>(GymErrors.NotFound) : Result.Success(item);
    }

    public async Task<Result<int>> Handle(CountUnreadCommunicationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<int>(scope.Error);
        }

        return Result.Success(await _communications.CountUnreadAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<CommunicationInboxSummaryResponse>> Handle(GetCommunicationInboxSummaryQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<CommunicationInboxSummaryResponse>(scope.Error);
        }

        var unread = await _communications.CountUnreadAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        var latest = await _communications.LatestInboxActivityAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return Result.Success(new CommunicationInboxSummaryResponse(unread, latest, _clock.UtcNow));
    }

    public async Task<Result<CommunicationInboxSyncResponse>> Handle(SyncInboxCommunicationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<CommunicationInboxSyncResponse>(scope.Error);
        }

        var since = request.SinceUtc ?? DateTime.MinValue;
        var take = request.Take <= 0 ? 30 : request.Take;
        var items = await _communications.SyncInboxAsync(scope.Value.TenantId, scope.Value.UserId, since, _clock.UtcNow, take, cancellationToken).ConfigureAwait(false);
        var unread = await _communications.CountUnreadAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return Result.Success(new CommunicationInboxSyncResponse(items, unread, _clock.UtcNow));
    }

    public async Task<Result> Handle(MarkInboxCommunicationReadCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        var recipient = await _communications.GetRecipientAsync(scope.Value.TenantId, scope.Value.UserId, request.Id, cancellationToken).ConfigureAwait(false);
        if (recipient is null)
        {
            return Result.Failure(GymErrors.NotFound);
        }

        recipient.MarkRead(_clock.UtcNow);
        if (!await _communications.HasReadAsync(scope.Value.TenantId, request.Id, scope.Value.UserId, cancellationToken).ConfigureAwait(false))
        {
            await _communications.MarkReadAsync(CommunicationRead.Create(Guid.NewGuid(), scope.Value.TenantId, request.Id, scope.Value.UserId, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> Handle(MarkAllInboxCommunicationsReadCommand request, CancellationToken cancellationToken)
    {
        var scope = await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope;
        }

        await _communications.MarkAllReadAsync(scope.Value.TenantId, scope.Value.UserId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<(Guid TenantId, Guid UserId)>> OpenAsync(CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.CommunicationRead, FeatureCodes.GymCommunications, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(gate.Error);
        }

        return Result.Success((tenantId, userId));
    }
}
