using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Chat;

public sealed class ChatPermissionRule : BaseEntity
{
    private ChatPermissionRule()
    {
    }

    public Guid TenantId { get; private set; }

    public string RuleCode { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public ChatRelationshipScope Scope { get; private set; }

    public bool AllowImages { get; private set; }

    public bool AllowDocuments { get; private set; }

    public static ChatPermissionRule Create(Guid id, Guid tenantId, string ruleCode, bool enabled, ChatRelationshipScope scope, bool allowImages, bool allowDocuments) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            RuleCode = ruleCode,
            Enabled = enabled,
            Scope = scope,
            AllowImages = allowImages,
            AllowDocuments = allowDocuments,
        };

    public void Update(bool enabled, ChatRelationshipScope scope, bool allowImages, bool allowDocuments)
    {
        Enabled = enabled;
        Scope = scope;
        AllowImages = allowImages;
        AllowDocuments = allowDocuments;
    }
}

public sealed class ChatConversation : BaseEntity
{
    private ChatConversation()
    {
    }

    public Guid TenantId { get; private set; }

    public ChatConversationType ConversationType { get; private set; }

    public string PairKey { get; private set; } = string.Empty;

    public Guid? LastMessageId { get; private set; }

    public DateTime? LastMessageAt { get; private set; }

    public string? LastPreview { get; private set; }

    public static string DirectPairKey(Guid leftUserId, Guid rightUserId)
    {
        var low = leftUserId.CompareTo(rightUserId) <= 0 ? leftUserId : rightUserId;
        var high = leftUserId.CompareTo(rightUserId) <= 0 ? rightUserId : leftUserId;
        return $"{low:D}:{high:D}";
    }

    public static ChatConversation CreateDirect(Guid id, Guid tenantId, Guid leftUserId, Guid rightUserId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            ConversationType = ChatConversationType.Direct,
            PairKey = DirectPairKey(leftUserId, rightUserId),
        };

    public void RecordLastMessage(Guid messageId, DateTime createdAt, string preview)
    {
        LastMessageId = messageId;
        LastMessageAt = createdAt;
        LastPreview = preview.Length <= 200 ? preview : preview[..200];
    }
}

public sealed class ChatParticipant : BaseEntity
{
    private ChatParticipant()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public Guid? LastReadMessageId { get; private set; }

    public DateTime? LastReadAt { get; private set; }

    public bool IsMuted { get; private set; }

    public bool IsArchived { get; private set; }

    public static ChatParticipant Create(Guid id, Guid tenantId, Guid conversationId, Guid userId, DateTime joinedAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = joinedAt,
        };

    public void MarkRead(Guid messageId, DateTime readAt)
    {
        if (LastReadAt is DateTime existing && existing > readAt)
        {
            return;
        }

        LastReadMessageId = messageId;
        LastReadAt = readAt;
    }
}

public sealed class ChatMessage : BaseEntity
{
    private ChatMessage()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public ChatMessageType MessageType { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public string? ClientMessageId { get; private set; }

    public static ChatMessage Create(Guid id, Guid tenantId, Guid conversationId, Guid senderUserId, ChatMessageType type, string body, string? clientMessageId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            MessageType = type,
            Body = body.Trim(),
            ClientMessageId = string.IsNullOrWhiteSpace(clientMessageId) ? null : clientMessageId.Trim(),
        };
}

public sealed class ChatAttachment : BaseEntity
{
    private ChatAttachment()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid StoredFileId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public static ChatAttachment Create(Guid id, Guid tenantId, Guid messageId, Guid storedFileId, string storageKey, string fileName, string contentType, long sizeBytes) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            MessageId = messageId,
            StoredFileId = storedFileId,
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
        };
}
