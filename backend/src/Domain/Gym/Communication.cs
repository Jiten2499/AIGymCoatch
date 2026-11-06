using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum CommunicationType
{
    Announcement = 0,
    Circular = 1,
    Offer = 2,
    Event = 3,
    Alert = 4,
    General = 5,
}

public enum CommunicationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

public enum CommunicationStatus
{
    Draft = 0,
    Scheduled = 1,
    Published = 2,
    Archived = 3,
}

public enum CommunicationAudienceType
{
    AllMembers = 0,
    ActiveMembers = 1,
    ExpiringMembers = 2,
    MembershipPackage = 3,
    TrainerMembers = 4,
    SelectedMembers = 5,
    AllTrainers = 6,
    AllStaff = 7,
    AllBusinessUsers = 8,
    SelectedTrainers = 9,
    SelectedStaff = 10,
}

public sealed class Communication : BaseEntity
{
    private Communication()
    {
    }

    public Guid TenantId { get; private set; }

    public CommunicationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public CommunicationPriority Priority { get; private set; }

    public CommunicationStatus Status { get; private set; }

    public CommunicationAudienceType AudienceType { get; private set; }

    public Guid? AudiencePackageId { get; private set; }

    public Guid? AudienceTrainerId { get; private set; }

    public int? ExpiringWithinDays { get; private set; }

    public string? ImageStorageKey { get; private set; }

    public string? ImageContentType { get; private set; }

    public DateTime? PublishAt { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public DateTime? ArchivedAt { get; private set; }

    public static Communication Create(
        Guid id,
        Guid tenantId,
        Guid createdByUserId,
        CommunicationType type,
        string title,
        string message,
        CommunicationPriority priority,
        CommunicationAudienceType audienceType,
        Guid? audiencePackageId,
        Guid? audienceTrainerId,
        int? expiringWithinDays,
        DateTime? publishAt,
        DateTime? expiresAt)
    {
        var communication = new Communication
        {
            Id = id,
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Status = CommunicationStatus.Draft,
        };
        communication.ApplyContent(type, title, message, priority, audienceType, audiencePackageId, audienceTrainerId, expiringWithinDays, publishAt, expiresAt);
        return communication;
    }

    public bool CanEdit => Status is CommunicationStatus.Draft or CommunicationStatus.Scheduled;

    public void UpdateDraft(
        CommunicationType type,
        string title,
        string message,
        CommunicationPriority priority,
        CommunicationAudienceType audienceType,
        Guid? audiencePackageId,
        Guid? audienceTrainerId,
        int? expiringWithinDays,
        DateTime? publishAt,
        DateTime? expiresAt)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Only draft or scheduled communications can be edited.");
        }

        ApplyContent(type, title, message, priority, audienceType, audiencePackageId, audienceTrainerId, expiringWithinDays, publishAt, expiresAt);
    }

    public void SetImage(string storageKey, string contentType)
    {
        ImageStorageKey = storageKey;
        ImageContentType = contentType;
    }

    public void ClearImage()
    {
        ImageStorageKey = null;
        ImageContentType = null;
    }

    public void Publish(DateTime utcNow, bool immediate = false)
    {
        if (Status == CommunicationStatus.Published)
        {
            return;
        }

        if (Status is CommunicationStatus.Archived)
        {
            throw new InvalidOperationException("Archived communications cannot be published.");
        }

        if (!immediate && PublishAt is DateTime scheduled && scheduled > utcNow)
        {
            Status = CommunicationStatus.Scheduled;
            PublishedAt = null;
            return;
        }

        Status = CommunicationStatus.Published;
        if (PublishAt is null || PublishAt > utcNow)
        {
            PublishAt = utcNow;
        }

        PublishedAt = utcNow;
    }

    public void ReturnToDraft()
    {
        if (Status != CommunicationStatus.Scheduled)
        {
            return;
        }

        Status = CommunicationStatus.Draft;
        PublishedAt = null;
    }

    public void Archive(DateTime utcNow)
    {
        if (Status is CommunicationStatus.Draft)
        {
            throw new InvalidOperationException("Draft communications cannot be archived.");
        }

        Status = CommunicationStatus.Archived;
        ArchivedAt = utcNow;
    }

    public bool IsVisibleToMembers(DateTime utcNow) =>
        Status == CommunicationStatus.Published
        && PublishAt is DateTime published
        && published <= utcNow
        && (ExpiresAt is null || ExpiresAt > utcNow);

    private void ApplyContent(
        CommunicationType type,
        string title,
        string message,
        CommunicationPriority priority,
        CommunicationAudienceType audienceType,
        Guid? audiencePackageId,
        Guid? audienceTrainerId,
        int? expiringWithinDays,
        DateTime? publishAt,
        DateTime? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Title and message are required.");
        }

        if (expiresAt is DateTime end && publishAt is DateTime start && end < start)
        {
            throw new ArgumentException("Expiry must be on or after the publish time.");
        }

        Type = type;
        Title = title.Trim();
        Message = message.Trim();
        Priority = priority;
        AudienceType = audienceType;
        AudiencePackageId = audienceType == CommunicationAudienceType.MembershipPackage ? audiencePackageId : null;
        AudienceTrainerId = audienceType == CommunicationAudienceType.TrainerMembers ? audienceTrainerId : null;
        ExpiringWithinDays = audienceType == CommunicationAudienceType.ExpiringMembers
            ? Math.Clamp(expiringWithinDays ?? 7, 1, 90)
            : null;
        PublishAt = publishAt;
        ExpiresAt = expiresAt;
    }
}

public sealed class CommunicationAudienceMember : BaseEntity
{
    private CommunicationAudienceMember()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CommunicationId { get; private set; }

    public Guid MemberProfileId { get; private set; }

    public static CommunicationAudienceMember Create(Guid id, Guid tenantId, Guid communicationId, Guid memberProfileId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CommunicationId = communicationId,
            MemberProfileId = memberProfileId,
        };
}

public sealed class CommunicationRead : BaseEntity
{
    private CommunicationRead()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CommunicationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime ReadAt { get; private set; }

    public static CommunicationRead Create(Guid id, Guid tenantId, Guid communicationId, Guid userId, DateTime utcNow) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CommunicationId = communicationId,
            UserId = userId,
            ReadAt = utcNow,
        };
}
