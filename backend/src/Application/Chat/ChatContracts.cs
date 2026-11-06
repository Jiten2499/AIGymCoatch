using LifeFitAI.Domain.Chat;

namespace LifeFitAI.Application.Chat;

public sealed record ChatPerson(
    Guid UserId,
    string DisplayName,
    ChatPartyRole Role,
    Guid? MemberProfileId,
    Guid? TrainerProfileId,
    Guid? AssignedTrainerProfileId,
    bool HasPhoto);

public sealed record ChatRuleResponse(string Code, bool Enabled, ChatRelationshipScope Scope, bool AllowImages, bool AllowDocuments);

public sealed record ChatInboxItem(
    Guid ConversationId,
    Guid OtherUserId,
    string DisplayName,
    ChatPartyRole Role,
    string? LastPreview,
    DateTime? LastMessageAt,
    int UnreadCount,
    bool HasPhoto);

public sealed record ChatPhotoFile(byte[] Content, string ContentType);

public sealed record ChatMessageItem(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    ChatMessageType MessageType,
    string Body,
    DateTime CreatedAt,
    bool IsReadByOther,
    Guid? AttachmentId,
    string? FileName,
    string? ContentType,
    long? SizeBytes);

public sealed record ChatSummaryResponse(int UnreadConversations, int UnreadMessages);

public sealed record ChatAttachmentContent(byte[] Content, string ContentType, string FileName);

public static class ChatErrors
{
    public static readonly SharedKernel.Results.Error Forbidden = new("chat.forbidden", "Chat is not allowed for these people.");
    public static readonly SharedKernel.Results.Error NotFound = new("chat.not_found", "The conversation was not found.");
    public static readonly SharedKernel.Results.Error InvalidMessage = new("chat.invalid_message", "Enter a message or an allowed attachment.");
    public static readonly SharedKernel.Results.Error InvalidAttachment = new("chat.invalid_attachment", "This attachment type or size is not allowed.");
}
