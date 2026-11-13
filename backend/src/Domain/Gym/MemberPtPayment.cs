using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class MemberPtPayment : BaseEntity
{
    private MemberPtPayment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid MemberPtAssignmentId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public MembershipPaymentMethod PaymentMethod { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public string? Notes { get; private set; }

    public MembershipPaymentStatus Status { get; private set; }

    public Guid ReceivedByUserId { get; private set; }

    public static MemberPtPayment Create(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        Guid assignmentId,
        decimal amount,
        string currency,
        MembershipPaymentMethod method,
        DateOnly date,
        Guid receivedByUserId,
        string? reference,
        string? notes)
    {
        if (!MembershipPaymentRules.Amount(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (MembershipPaymentRules.RequiresReference(method) && string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A reference number is required for this payment method.", nameof(reference));
        }

        var cleanedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        var cleanedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (cleanedReference is { Length: > 100 } || cleanedNotes is { Length: > 2000 })
        {
            throw new ArgumentException("Payment details are too long.");
        }

        return new MemberPtPayment
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberProfileId,
            MemberPtAssignmentId = assignmentId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            PaymentMethod = method,
            PaymentDate = date,
            ReferenceNumber = cleanedReference,
            Notes = cleanedNotes,
            Status = MembershipPaymentStatus.Posted,
            ReceivedByUserId = receivedByUserId,
        };
    }
}
