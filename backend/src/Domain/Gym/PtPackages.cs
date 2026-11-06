using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum PtPaymentStatus
{
    Unpaid = 1,
}

public enum PtPackageStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
}

public enum MemberPtAssignmentStatus
{
    PendingPayment = 1,
    Active = 2,
    Exhausted = 3,
    Expired = 4,
    Cancelled = 5,
}

public sealed class PtPackage : BaseEntity
{
    private PtPackage()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int SessionCount { get; private set; }

    public int ValidityDays { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "USD";

    public bool IsActive { get; private set; }

    public PtPackageStatus Status { get; private set; }

    public static PtPackage Create(Guid id, Guid tenantId, string name, string? description, int sessionCount, int validityDays, decimal price, string currency)
    {
        return new PtPackage
        {
            Id = id,
            TenantId = tenantId,
            Name = CleanName(name),
            Description = CleanDescription(description),
            SessionCount = RequireCount(sessionCount, nameof(sessionCount)),
            ValidityDays = RequireCount(validityDays, nameof(validityDays)),
            Price = RequirePrice(price),
            Currency = CleanCurrency(currency),
            IsActive = true,
            Status = PtPackageStatus.Active,
        };
    }

    public void Update(string name, string? description, int sessionCount, int validityDays, decimal price, string currency)
    {
        Name = CleanName(name);
        Description = CleanDescription(description);
        SessionCount = RequireCount(sessionCount, nameof(sessionCount));
        ValidityDays = RequireCount(validityDays, nameof(validityDays));
        Price = RequirePrice(price);
        Currency = CleanCurrency(currency);
    }

    public void Activate()
    {
        IsActive = true;
        Status = PtPackageStatus.Active;
    }

    public void Deactivate()
    {
        IsActive = false;
        Status = PtPackageStatus.Inactive;
    }

    public void MarkDraft()
    {
        IsActive = false;
        Status = PtPackageStatus.Draft;
    }

    private static string CleanName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 150)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "A personal-training package name must be 1 to 150 characters.");
        }

        return trimmed;
    }

    private static string? CleanDescription(string? description)
    {
        var trimmed = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmed is { Length: > 1000 })
        {
            throw new ArgumentOutOfRangeException(nameof(description), "A personal-training package description must be 1000 characters or fewer.");
        }

        return trimmed;
    }

    private static string CleanCurrency(string currency)
    {
        var trimmed = currency.Trim().ToUpperInvariant();
        if (trimmed.Length != 3)
        {
            throw new ArgumentOutOfRangeException(nameof(currency), "Currency must be a three-letter code.");
        }

        return trimmed;
    }

    private static int RequireCount(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Session count and validity must be positive.");
        }

        return value;
    }

    private static decimal RequirePrice(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Package price cannot be negative.");
        }

        return price;
    }
}

public sealed class MemberPtAssignment : BaseEntity
{
    private MemberPtAssignment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid PtPackageId { get; private set; }

    public Guid? TrainerProfileId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public int SessionCount { get; private set; }

    public int ValidityDays { get; private set; }

    public string PackageName { get; private set; } = string.Empty;

    public int SessionsRemaining { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "USD";

    public PtPaymentStatus PaymentStatus { get; private set; }

    public MemberPtAssignmentStatus Status { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public static MemberPtAssignment Open(Guid id, PtPackage package, Guid memberProfileId, Guid? trainerProfileId, DateOnly start)
    {
        return new MemberPtAssignment
        {
            Id = id,
            TenantId = package.TenantId,
            MemberProfileId = memberProfileId,
            PtPackageId = package.Id,
            TrainerProfileId = trainerProfileId,
            StartDate = start,
            EndDate = start.AddDays(package.ValidityDays - 1),
            SessionCount = package.SessionCount,
            ValidityDays = package.ValidityDays,
            PackageName = package.Name,
            SessionsRemaining = package.SessionCount,
            Price = package.Price,
            Currency = package.Currency,
            PaymentStatus = PtPaymentStatus.Unpaid,
            Status = MemberPtAssignmentStatus.Active,
        };
    }

    public bool TryConsume(DateOnly day)
    {
        if (day > EndDate && Status is MemberPtAssignmentStatus.Active or MemberPtAssignmentStatus.PendingPayment)
        {
            Status = MemberPtAssignmentStatus.Expired;
            return false;
        }

        if (Status is not (MemberPtAssignmentStatus.Active or MemberPtAssignmentStatus.PendingPayment) || SessionsRemaining <= 0 || day < StartDate)
        {
            return false;
        }

        SessionsRemaining--;
        if (SessionsRemaining == 0)
        {
            Status = MemberPtAssignmentStatus.Exhausted;
        }

        return true;
    }

    public bool Restore()
    {
        if (SessionsRemaining >= SessionCount || Status is MemberPtAssignmentStatus.Cancelled)
        {
            return false;
        }

        SessionsRemaining++;
        if (Status == MemberPtAssignmentStatus.Exhausted)
        {
            Status = MemberPtAssignmentStatus.Active;
        }

        return true;
    }

    public void Cancel(DateTime utcNow, string? reason)
    {
        Status = MemberPtAssignmentStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
