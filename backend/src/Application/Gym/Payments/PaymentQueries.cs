using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Gym.Payments;

public sealed record GetMembershipPaymentQuery(Guid Id) : IRequest<Result<MembershipPaymentItem>>;

public sealed record ListMembershipPaymentsQuery(Guid MembershipId, int Page, int PageSize) : IRequest<Result<PageResult<MembershipPaymentItem>>>;

public sealed record GetMembershipPaymentSummaryQuery(Guid MembershipId) : IRequest<Result<MembershipPaymentSummary>>;

public sealed class PaymentQueryHandler :
    IRequestHandler<GetMembershipPaymentQuery, Result<MembershipPaymentItem>>,
    IRequestHandler<ListMembershipPaymentsQuery, Result<PageResult<MembershipPaymentItem>>>,
    IRequestHandler<GetMembershipPaymentSummaryQuery, Result<MembershipPaymentSummary>>
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IMembershipRepository _memberships;
    private readonly IMembershipPaymentRepository _payments;

    public PaymentQueryHandler(
        IOperationGuard guard,
        ICurrentActor actor,
        IMembershipRepository memberships,
        IMembershipPaymentRepository payments)
    {
        _guard = guard;
        _actor = actor;
        _memberships = memberships;
        _payments = payments;
    }

    public async Task<Result<MembershipPaymentItem>> Handle(GetMembershipPaymentQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MembershipPaymentItem>(gate.Error);
        }

        var item = await _payments.ReadAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        return item is null
            ? Result.Failure<MembershipPaymentItem>(GymErrors.NotFound)
            : Result.Success(item);
    }

    public async Task<Result<PageResult<MembershipPaymentItem>>> Handle(ListMembershipPaymentsQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<PageResult<MembershipPaymentItem>>(gate.Error);
        }

        if (await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<PageResult<MembershipPaymentItem>>(GymErrors.NotFound);
        }

        return Result.Success(await _payments.ListForMembershipAsync(tenantId, request.MembershipId, request.Page, request.PageSize, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<MembershipPaymentSummary>> Handle(GetMembershipPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(PermissionCodes.PaymentRead, FeatureCodes.GymPaymentManagement, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId)
        {
            return Result.Failure<MembershipPaymentSummary>(gate.Error);
        }

        var membership = await _memberships.GetMembershipAsync(tenantId, request.MembershipId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return Result.Failure<MembershipPaymentSummary>(GymErrors.NotFound);
        }

        var paid = await _payments.SumPostedAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(PaymentMapping.Summary(membership, paid));
    }
}

public static class PaymentMapping
{
    public static MembershipPaymentSummary Summary(GymMembership membership, decimal totalPaid) =>
        new(
            membership.Id,
            membership.MemberProfileId,
            membership.PackagePrice,
            membership.DiscountAmount,
            membership.FinalAmount,
            membership.Currency,
            membership.DiscountRuleId,
            membership.DiscountName,
            totalPaid,
            MembershipPaymentRules.Outstanding(membership.FinalAmount, totalPaid),
            MembershipPaymentRules.Settlement(membership.FinalAmount, totalPaid));
}
