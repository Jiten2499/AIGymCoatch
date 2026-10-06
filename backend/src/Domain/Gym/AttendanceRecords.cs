namespace LifeFitAI.Domain.Gym;

public sealed class AttendanceRecord
{
    private AttendanceRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public Guid GymMembershipId { get; private set; }

    public DateTime CheckInTime { get; private set; }

    public DateTime? CheckOutTime { get; private set; }

    public CheckInMethod CheckInMethod { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public bool IsOpen => CheckOutTime is null;

    public static AttendanceRecord CheckIn(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid memberProfileId,
        Guid gymMembershipId,
        CheckInMethod method,
        DateTime utcNow)
    {
        return new AttendanceRecord
        {
            Id = id,
            TenantId = tenantId,
            BranchId = branchId,
            MemberProfileId = memberProfileId,
            GymMembershipId = gymMembershipId,
            CheckInMethod = method,
            CheckInTime = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public bool CheckOut(DateTime utcNow)
    {
        if (CheckOutTime is not null || utcNow < CheckInTime)
        {
            return false;
        }

        CheckOutTime = utcNow;
        UpdatedAt = utcNow;
        return true;
    }
}

public sealed class QrCheckInToken
{
    private QrCheckInToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? UsedAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public bool IsUsable(DateTime utcNow) => UsedAt is null && RevokedAt is null && ExpiresAt > utcNow;

    public static QrCheckInToken Issue(
        Guid id,
        Guid tenantId,
        Guid memberProfileId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        Guid? createdByUserId)
    {
        return new QrCheckInToken
        {
            Id = id,
            TenantId = tenantId,
            MemberProfileId = memberProfileId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            CreatedByUserId = createdByUserId,
        };
    }

    public void MarkUsed(DateTime utcNow) => UsedAt = utcNow;
}
