using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Platform;

namespace LifeFitAI.Application.Chat;

public interface IChatRepository
{
    Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, ChatRuleState>> GetRulesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task SaveRulesAsync(Guid tenantId, IReadOnlyList<ChatRuleState> rules, CancellationToken cancellationToken);

    Task<ChatPerson?> FindPersonAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatPerson>> SearchPeopleAsync(Guid tenantId, string? term, int take, CancellationToken cancellationToken);

    Task<ChatConversation> GetOrCreateDirectAsync(Guid tenantId, Guid leftUserId, Guid rightUserId, CancellationToken cancellationToken);

    Task<ChatParticipant?> GetParticipantAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<ChatPerson?> OtherPersonAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatInboxItem>> ListInboxAsync(Guid tenantId, Guid userId, DateTime? before, int take, string? term, CancellationToken cancellationToken);

    Task<ChatMessage?> FindClientMessageAsync(Guid tenantId, Guid senderUserId, string clientMessageId, CancellationToken cancellationToken);

    Task AddMessageAsync(ChatConversation conversation, ChatMessage message, ChatAttachment? attachment, CancellationToken cancellationToken);

    Task<ChatConversation?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessageItem>> ListMessagesAsync(Guid tenantId, Guid conversationId, Guid readerUserId, DateTime? before, Guid? beforeId, int take, CancellationToken cancellationToken);

    Task MarkReadAsync(Guid tenantId, Guid conversationId, Guid userId, Guid messageId, CancellationToken cancellationToken);

    Task<ChatSummaryResponse> SummarizeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    Task<(ChatAttachment Attachment, Guid ConversationId)?> FindAttachmentAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken);

    Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken);

    Task AddOutboxAsync(OutboxMessage message, CancellationToken cancellationToken);

    Task<(string Key, string ContentType)?> FindPortraitAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public interface IChatNotifier
{
    Task MessageReceivedAsync(Guid conversationId, ChatMessageItem message, CancellationToken cancellationToken);

    Task MessageReadAsync(Guid conversationId, Guid userId, Guid messageId, DateTime readAt, CancellationToken cancellationToken);

    Task ConversationUpdatedAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
}

public interface IChatPresence
{
    void Connected(Guid userId, string connectionId);

    void Disconnected(string connectionId);

    void Watch(Guid userId, Guid conversationId, string connectionId);

    void Unwatch(string connectionId);

    bool IsWatching(Guid userId, Guid conversationId);
}

public sealed class NoopChatNotifier : IChatNotifier
{
    public Task MessageReceivedAsync(Guid conversationId, ChatMessageItem message, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MessageReadAsync(Guid conversationId, Guid userId, Guid messageId, DateTime readAt, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ConversationUpdatedAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) => Task.CompletedTask;
}
