using LifeFitAI.Application.Authorization;
using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Members;

public sealed record MemberPtAssignmentItem(
    Guid Id,
    Guid PackageId,
    string PackageName,
    Guid? TrainerId,
    DateOnly StartDate,
    DateOnly EndDate,
    int SessionCount,
    int SessionsRemaining,
    decimal Price,
    string Currency,
    MemberPtAssignmentStatus Status,
    PtPaymentStatus PaymentStatus,
    decimal AmountPaid,
    MembershipPaymentMethod? PaymentMethod,
    DateOnly? PaymentDate,
    string? PaymentReferenceNumber);

public sealed record MemberPtSessionItem(
    Guid Id,
    Guid TrainerId,
    DateTime ScheduledStartUtc,
    DateOnly? CompletedOn,
    PtSessionStatus Status,
    string? Notes,
    Guid? ConsumptionId,
    PtConsumptionStatus? ConsumptionStatus);

public sealed record ListMemberPtAssignmentsQuery(Guid MemberId) : IRequest<Result<IReadOnlyList<MemberPtAssignmentItem>>>;

public sealed record ListMemberPtSessionsQuery(Guid MemberId) : IRequest<Result<IReadOnlyList<MemberPtSessionItem>>>;

public sealed class MemberPtQueryHandlers :
    IRequestHandler<ListMemberPtAssignmentsQuery, Result<IReadOnlyList<MemberPtAssignmentItem>>>,
    IRequestHandler<ListMemberPtSessionsQuery, Result<IReadOnlyList<MemberPtSessionItem>>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IPeopleRepository _people;
    private readonly IPtPackageRepository _packages;

    public MemberPtQueryHandlers(IOperationGuard guard, ICurrentActor actor, IPeopleRepository people, IPtPackageRepository packages)
    {
        _guard = guard;
        _actor = actor;
        _people = people;
        _packages = packages;
    }

    public async Task<Result<IReadOnlyList<MemberPtAssignmentItem>>> Handle(ListMemberPtAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await RequireMemberAsync(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (tenant.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MemberPtAssignmentItem>>(tenant.Error);
        }

        var rows = await _packages.ListMemberAssignmentsAsync(tenant.Value, request.MemberId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MemberPtAssignmentItem> items = rows
            .Select(row => new MemberPtAssignmentItem(row.Id, row.PtPackageId, row.PackageName, row.TrainerProfileId, row.StartDate, row.EndDate, row.SessionCount, row.SessionsRemaining, row.Price, row.Currency, row.Status, row.PaymentStatus, row.AmountPaid, row.PaymentMethod, row.PaymentDate, row.PaymentReferenceNumber))
            .ToArray();
        return Result.Success(items);
    }

    public async Task<Result<IReadOnlyList<MemberPtSessionItem>>> Handle(ListMemberPtSessionsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await RequireMemberAsync(request.MemberId, cancellationToken).ConfigureAwait(false);
        if (tenant.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MemberPtSessionItem>>(tenant.Error);
        }

        var sessions = await _packages.ListMemberSessionsAsync(tenant.Value, request.MemberId, cancellationToken).ConfigureAwait(false);
        var consumptions = await _packages.ListMemberConsumptionsAsync(tenant.Value, request.MemberId, cancellationToken).ConfigureAwait(false);
        var bySession = consumptions.GroupBy(item => item.PtSessionId).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ConsumedAtUtc).First());
        IReadOnlyList<MemberPtSessionItem> items = sessions
            .Select(session =>
            {
                bySession.TryGetValue(session.Id, out var consumption);
                return new MemberPtSessionItem(session.Id, session.TrainerProfileId, session.ScheduledStartUtc, session.CompletedOn, session.Status, session.Notes, consumption?.Id, consumption?.Status);
            })
            .ToArray();
        return Result.Success(items);
    }

    private async Task<Result<Guid>> RequireMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.MemberRead, FeatureCodes.MemberManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<Guid>(gate.Error);
        }

        var member = await _people.GetMemberAsync(tenantId, memberId, cancellationToken).ConfigureAwait(false);
        return member is null ? Result.Failure<Guid>(GymErrors.NotFound) : Result.Success(tenantId);
    }
}
