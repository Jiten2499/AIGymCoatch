using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Payments;

public sealed record MembershipPaymentItem(
    Guid Id,
    Guid MemberProfileId,
    Guid GymMembershipId,
    decimal Amount,
    string Currency,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Notes,
    MembershipPaymentStatus Status,
    Guid ReceivedByUserId,
    string? ReceivedBy,
    DateTime CreatedAt,
    DateTime? VoidedAt);

public sealed record MembershipPaymentSummary(
    Guid MembershipId,
    Guid MemberProfileId,
    decimal PackagePrice,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Currency,
    Guid? DiscountRuleId,
    string? DiscountName,
    decimal TotalPaid,
    decimal Outstanding,
    MembershipSettlementStatus SettlementStatus);

public sealed record InitialMembershipPayment(
    decimal Amount,
    MembershipPaymentMethod PaymentMethod,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Notes);
