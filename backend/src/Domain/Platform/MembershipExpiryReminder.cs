namespace LifeFitAI.Domain.Platform;

public sealed class MembershipExpiryReminder
{
    private MembershipExpiryReminder()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid MembershipId { get; private set; }

    public string ReminderType { get; private set; } = string.Empty;

    public DateOnly ExpiryDate { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static MembershipExpiryReminder Create(Guid id, Guid tenantId, Guid membershipId, string reminderType, DateOnly expiryDate, DateTime createdAtUtc) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            MembershipId = membershipId,
            ReminderType = reminderType,
            ExpiryDate = expiryDate,
            CreatedAtUtc = createdAtUtc,
        };
}
