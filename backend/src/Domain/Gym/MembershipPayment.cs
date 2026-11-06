using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum MembershipPaymentStatus
{
    Posted = 1,
    Voided = 2,
}

public enum MembershipPaymentMethod
{
    Cash = 1,
    Upi = 2,
    DebitCard = 3,
    CreditCard = 4,
    BankTransfer = 5,
    OnlinePayment = 6,
    Cheque = 7,
    Other = 8,
}

public enum MembershipSettlementStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
}

public static class MembershipPaymentRules
{
    public static bool Amount(decimal amount) => amount > 0 && amount <= 999_999_999.9999m;

    public static bool Date(DateOnly paymentDate, DateOnly localToday) =>
        paymentDate >= new DateOnly(2000, 1, 1) && paymentDate <= localToday.AddDays(7);

    public static bool RequiresReference(MembershipPaymentMethod method) =>
        method is MembershipPaymentMethod.Upi
            or MembershipPaymentMethod.DebitCard
            or MembershipPaymentMethod.CreditCard
            or MembershipPaymentMethod.BankTransfer
            or MembershipPaymentMethod.OnlinePayment
            or MembershipPaymentMethod.Cheque;

    public static MembershipSettlementStatus Settlement(decimal finalAmount, decimal totalPaid)
    {
        if (totalPaid <= 0)
        {
            return MembershipSettlementStatus.Unpaid;
        }

        return totalPaid >= finalAmount
            ? MembershipSettlementStatus.Paid
            : MembershipSettlementStatus.PartiallyPaid;
    }

    public static decimal Outstanding(decimal finalAmount, decimal totalPaid) =>
        Math.Max(0m, decimal.Round(finalAmount - totalPaid, 4, MidpointRounding.AwayFromZero));
}

public static class MembershipPricing
{
    public static (decimal DiscountAmount, decimal FinalAmount, string? DiscountName) Snapshot(
        decimal packagePrice,
        DiscountRule? rule,
        DateOnly asOf)
    {
        if (rule is null || !rule.IsActive)
        {
            return (0m, packagePrice, null);
        }

        if (rule.StartDate is DateOnly start && asOf < start)
        {
            return (0m, packagePrice, null);
        }

        if (rule.EndDate is DateOnly end && asOf > end)
        {
            return (0m, packagePrice, null);
        }

        var discount = rule.DiscountType == DiscountType.Percentage
            ? decimal.Round(packagePrice * rule.Value / 100m, 4, MidpointRounding.AwayFromZero)
            : rule.Value;
        if (rule.MaximumDiscountAmount is decimal max)
        {
            discount = Math.Min(discount, max);
        }

        discount = Math.Clamp(discount, 0m, packagePrice);
        return (discount, packagePrice - discount, rule.Name);
    }

    public static bool AppliesToPackage(DiscountRule rule, MembershipPlan plan)
    {
        if (rule.MinimumPackagePrice is decimal minPrice && plan.Price < minPrice)
        {
            return false;
        }

        if (rule.MinimumDurationDays is int minDays && plan.DurationDays < minDays)
        {
            return false;
        }

        return rule.AppliesTo switch
        {
            DiscountAppliesTo.AllPackages => true,
            DiscountAppliesTo.SpecificPackage => rule.PackageId == plan.Id,
            DiscountAppliesTo.PackageCategory => rule.PackageCategory == plan.Category,
            DiscountAppliesTo.MemberCategory => false,
            _ => false,
        };
    }
}

public sealed class MembershipPayment : BaseEntity
{
    private MembershipPayment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid GymMembershipId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public MembershipPaymentMethod PaymentMethod { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public string? Notes { get; private set; }

    public MembershipPaymentStatus Status { get; private set; }

    public Guid ReceivedByUserId { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedByUserId { get; private set; }

    public static MembershipPayment Create(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        Guid gymMembershipId,
        decimal amount,
        string currency,
        MembershipPaymentMethod paymentMethod,
        DateOnly paymentDate,
        Guid receivedByUserId,
        string? referenceNumber,
        string? notes)
    {
        if (!MembershipPaymentRules.Amount(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (MembershipPaymentRules.RequiresReference(paymentMethod) && string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new ArgumentException("A reference number is required for this payment method.", nameof(referenceNumber));
        }

        return new MembershipPayment
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberProfileId,
            GymMembershipId = gymMembershipId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            PaymentMethod = paymentMethod,
            PaymentDate = paymentDate,
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = MembershipPaymentStatus.Posted,
            ReceivedByUserId = receivedByUserId,
        };
    }

    public bool Void(Guid userId, DateTime utcNow)
    {
        if (Status != MembershipPaymentStatus.Posted)
        {
            return false;
        }

        Status = MembershipPaymentStatus.Voided;
        VoidedAt = utcNow;
        VoidedByUserId = userId;
        return true;
    }
}
