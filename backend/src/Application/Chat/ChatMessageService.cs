using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Platform;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Chat;

public sealed partial class ChatMessageService
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IChatRepository _chats;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IChatNotifier _notifier;
    private readonly IChatPresence _presence;
    private readonly IFileStorageService _files;
    private readonly IFileStorage _portraits;

    public ChatMessageService(
        IOperationGuard guard,
        ICurrentActor actor,
        IChatRepository chats,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IChatNotifier notifier,
        IChatPresence presence,
        IFileStorageService files,
        IFileStorage portraits)
    {
        _guard = guard;
        _actor = actor;
        _chats = chats;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _notifier = notifier;
        _presence = presence;
        _files = files;
        _portraits = portraits;
    }

    public async Task<Result<ChatInboxItem>> OpenDirectAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatSend, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChatInboxItem>(scope.Error);
        }

        var access = await AuthorizePeopleAsync(scope.Value.TenantId, scope.Value.UserId, recipientUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<ChatInboxItem>(access.Error);
        }

        var conversation = await _chats.GetOrCreateDirectAsync(scope.Value.TenantId, scope.Value.UserId, recipientUserId, cancellationToken).ConfigureAwait(false);
        var inbox = await _chats.ListInboxAsync(scope.Value.TenantId, scope.Value.UserId, null, 50, null, cancellationToken).ConfigureAwait(false);
        var item = inbox.FirstOrDefault(row => row.ConversationId == conversation.Id);
        return item is null
            ? Result.Success(new ChatInboxItem(conversation.Id, recipientUserId, access.Value.Recipient.DisplayName, access.Value.Recipient.Role, null, null, 0, access.Value.Recipient.HasPhoto))
            : Result.Success(item);
    }

    public async Task<Result<ChatMessageItem>> SendTextAsync(Guid conversationId, string body, string? clientMessageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000)
        {
            return Result.Failure<ChatMessageItem>(ChatErrors.InvalidMessage);
        }

        var view = await RequireViewAsync(conversationId, PermissionCodes.ChatSend, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return Result.Failure<ChatMessageItem>(view.Error);
        }

        var existing = await ExistingAsync(view.Value, clientMessageId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(existing);
        }

        var message = ChatMessage.Create(Guid.NewGuid(), view.Value.TenantId, conversationId, view.Value.Sender.UserId, ChatMessageType.Text, body, clientMessageId);
        return await PersistAsync(view.Value, message, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ChatMessageItem>> SendAttachmentAsync(Guid conversationId, string fileName, string contentType, byte[] content, string? clientMessageId, CancellationToken cancellationToken)
    {
        var view = await RequireViewAsync(conversationId, PermissionCodes.ChatSend, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return Result.Failure<ChatMessageItem>(view.Error);
        }

        var kind = AttachmentKind(contentType);
        if (kind is null || content.Length == 0 || !StorageKeys.IsAllowedSize(content.Length) || !StorageKeys.IsAllowedContentType(contentType))
        {
            return Result.Failure<ChatMessageItem>(ChatErrors.InvalidAttachment);
        }

        if (kind == ChatMessageType.Image && !view.Value.Access.AllowImages)
        {
            return Result.Failure<ChatMessageItem>(ChatErrors.InvalidAttachment);
        }

        if (kind == ChatMessageType.Document && !view.Value.Access.AllowDocuments)
        {
            return Result.Failure<ChatMessageItem>(ChatErrors.InvalidAttachment);
        }

        var existing = await ExistingAsync(view.Value, clientMessageId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(existing);
        }

        var fileId = Guid.NewGuid();
        var key = StorageKeys.ChatKey(view.Value.TenantId, conversationId, fileId);
        var uploaded = await _files.UploadAsync(new FileUploadRequest(view.Value.TenantId, view.Value.Sender.UserId, key, fileName, contentType, content, "ChatMessage", conversationId), cancellationToken).ConfigureAwait(false);
        if (uploaded.IsFailure)
        {
            return Result.Failure<ChatMessageItem>(uploaded.Error);
        }

        var message = ChatMessage.Create(Guid.NewGuid(), view.Value.TenantId, conversationId, view.Value.Sender.UserId, kind.Value, fileName, clientMessageId);
        var attachment = ChatAttachment.Create(Guid.NewGuid(), view.Value.TenantId, message.Id, uploaded.Value.Id, uploaded.Value.LogicalKey, uploaded.Value.FileName, uploaded.Value.ContentType, uploaded.Value.FileSize);
        return await PersistAsync(view.Value, message, attachment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> MarkReadAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var view = await RequireViewAsync(conversationId, PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return view;
        }

        await _chats.MarkReadAsync(view.Value.TenantId, conversationId, view.Value.Sender.UserId, messageId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.MessageReadAsync(conversationId, view.Value.Sender.UserId, messageId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await _notifier.ConversationUpdatedAsync(view.Value.Recipient.UserId, conversationId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<ChatMessageItem?> ExistingAsync(ChatView view, string? clientMessageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientMessageId))
        {
            return null;
        }

        var found = await _chats.FindClientMessageAsync(view.TenantId, view.Sender.UserId, clientMessageId.Trim(), cancellationToken).ConfigureAwait(false);
        if (found is null || found.ConversationId != view.Conversation.Id)
        {
            return null;
        }

        var page = await _chats.ListMessagesAsync(view.TenantId, view.Conversation.Id, view.Sender.UserId, null, null, 1, cancellationToken).ConfigureAwait(false);
        return page.FirstOrDefault(item => item.Id == found.Id) ?? new ChatMessageItem(found.Id, found.ConversationId, found.SenderUserId, found.MessageType, found.Body, found.CreatedAt, false, null, null, null, null);
    }

    private async Task<Result<ChatMessageItem>> PersistAsync(ChatView view, ChatMessage message, ChatAttachment? attachment, CancellationToken cancellationToken)
    {
        view.Conversation.RecordLastMessage(message.Id, _clock.UtcNow, message.Body);
        await _chats.AddMessageAsync(view.Conversation, message, attachment, cancellationToken).ConfigureAwait(false);
        if (!_presence.IsWatching(view.Recipient.UserId, view.Conversation.Id))
        {
            var title = view.Sender.DisplayName;
            var preview = message.Body.Length <= 180 ? message.Body : message.Body[..180];
            await _chats.AddNotificationAsync(Notification.Create(Guid.NewGuid(), view.Recipient.UserId, view.TenantId, NotificationType.ChatMessage, title, preview, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        }

        var payload = $"{{\"messageId\":\"{message.Id:D}\",\"conversationId\":\"{view.Conversation.Id:D}\",\"recipientUserId\":\"{view.Recipient.UserId:D}\"}}";
        await _chats.AddOutboxAsync(OutboxMessage.Create(Guid.NewGuid(), view.TenantId, "Chat.Message", payload, _clock.UtcNow, null, $"Chat.Message:{message.Id:D}", _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var item = new ChatMessageItem(message.Id, message.ConversationId, message.SenderUserId, message.MessageType, message.Body, _clock.UtcNow, false, attachment?.Id, attachment?.FileName, attachment?.ContentType, attachment?.SizeBytes);
        await _notifier.MessageReceivedAsync(view.Conversation.Id, item, cancellationToken).ConfigureAwait(false);
        await _notifier.ConversationUpdatedAsync(view.Sender.UserId, view.Conversation.Id, cancellationToken).ConfigureAwait(false);
        await _notifier.ConversationUpdatedAsync(view.Recipient.UserId, view.Conversation.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(item);
    }

    private static ChatMessageType? AttachmentKind(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/png" or "image/webp" => ChatMessageType.Image,
        "application/pdf" or "text/plain" => ChatMessageType.Document,
        _ => null,
    };

    private sealed record ChatView(Guid TenantId, ChatConversation Conversation, ChatPerson Sender, ChatPerson Recipient, ChatAccessDecision Access);
}
