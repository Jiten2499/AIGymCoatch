using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Ledger;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Members;

internal static class MemberMembershipOpening
{
    public static async Task OpenAsync(
        IMembershipRepository plans,
        IMembershipPaymentRepository payments,
        ILedgerPostingService ledger,
        IAuditLogRepository audit,
        IClientContext client,
        IDateTimeProvider clock,
        Guid tenantId,
        Guid actorId,
        Guid memberProfileId,
        Guid? branchId,
        MembershipPlan plan,
        DateOnly start,
        DateOnly today,
        bool autoRenew,
        InitialMembershipPayment? paymentRequest,
        CancellationToken cancellationToken)
    {
        var membership = GymMembership.Create(Guid.NewGuid(), tenantId, memberProfileId, branchId, plan.Id, $"G{Guid.NewGuid():N}"[..9].ToUpperInvariant(), start, plan.InclusiveEnd(start), today, autoRenew, null);
        membership.SetPricing(plan.Price, 0m, plan.Price, plan.Currency, null, null);
        membership.ApplyIncludedSessions(plan.IncludedPtSessions);
        if (await plans.OverlapsMembershipAsync(tenantId, memberProfileId, membership.StartDate, membership.EndDate, null, cancellationToken).ConfigureAwait(false))
        {
            throw new MemberOnboardingException(GymErrors.Conflict);
        }

        await plans.AddMembershipAsync(membership, cancellationToken).ConfigureAwait(false);
        await plans.AddHistoryAsync(MembershipHistoryEntry.Create(Guid.NewGuid(), tenantId, membership.Id, MembershipAction.Created, null, membership.Status, start, null, actorId, clock.UtcNow), cancellationToken).ConfigureAwait(false);

        if (paymentRequest is null)
        {
            return;
        }

        if (paymentRequest.Amount > membership.FinalAmount)
        {
            throw new MemberOnboardingException(GymErrors.InvalidState);
        }

        MembershipPayment payment;
        try
        {
            payment = MembershipPayment.Create(
                Guid.NewGuid(),
                tenantId,
                memberProfileId,
                membership.Id,
                paymentRequest.Amount,
                membership.Currency,
                paymentRequest.PaymentMethod,
                paymentRequest.PaymentDate,
                actorId,
                paymentRequest.ReferenceNumber,
                paymentRequest.Notes);
        }
        catch (ArgumentException)
        {
            throw new MemberOnboardingException(GymErrors.InvalidState);
        }

        await payments.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        var ledgerPost = await ledger.TryPostMembershipPaymentAsync(tenantId, payment, actorId, cancellationToken).ConfigureAwait(false);
        if (ledgerPost.IsFailure)
        {
            throw new MemberOnboardingException(ledgerPost.Error);
        }

        await audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, actorId, "MembershipPaymentRecorded", "MembershipPayment", payment.Id, membership.Id.ToString(), $"{payment.Amount} {payment.Currency} {payment.PaymentMethod}", client.IpAddress, client.UserAgent, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MemberOnboardingException : Exception
{
    public MemberOnboardingException(Error error) => Error = error;

    public Error Error { get; }
}
