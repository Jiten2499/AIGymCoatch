using LifeFitAI.Application.Identity;
using LifeFitAI.Application.Storage;
using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Chat;

public sealed partial class ChatMessageService
{
    public async Task<Result<IReadOnlyList<ChatInboxItem>>> ListInboxAsync(DateTime? before, int take, string? term, string? filter, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatInboxItem>>(scope.Error);
        }

        await _chats.EnsureDefaultsAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _chats.ListInboxAsync(scope.Value.TenantId, scope.Value.UserId, before, Math.Clamp(take, 1, 50), term, cancellationToken).ConfigureAwait(false);
        var allowed = new List<ChatInboxItem>();
        foreach (var row in rows)
        {
            var access = await AuthorizePeopleAsync(scope.Value.TenantId, scope.Value.UserId, row.OtherUserId, cancellationToken).ConfigureAwait(false);
            if (access.IsFailure)
            {
                continue;
            }

            var roleOk = filter switch
            {
                "trainers" => row.Role == ChatPartyRole.Trainer,
                "members" => row.Role == ChatPartyRole.Member,
                "unread" => row.UnreadCount > 0,
                _ => true,
            };
            if (roleOk)
            {
                allowed.Add(row);
            }
        }

        return Result.Success<IReadOnlyList<ChatInboxItem>>(allowed);
    }

    public async Task<Result<IReadOnlyList<ChatMessageItem>>> ListMessagesAsync(Guid conversationId, DateTime? before, Guid? beforeId, int take, CancellationToken cancellationToken)
    {
        var view = await RequireViewAsync(conversationId, PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatMessageItem>>(view.Error);
        }

        var page = await _chats.ListMessagesAsync(view.Value.TenantId, conversationId, view.Value.Sender.UserId, before, beforeId, Math.Clamp(take, 1, 50), cancellationToken).ConfigureAwait(false);
        return Result.Success(page);
    }

    public async Task<Result<IReadOnlyList<ChatPerson>>> SearchAsync(string? term, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatSend, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatPerson>>(scope.Error);
        }

        var people = await _chats.SearchPeopleAsync(scope.Value.TenantId, term, 200, cancellationToken).ConfigureAwait(false);
        var allowed = new List<ChatPerson>();
        foreach (var person in people)
        {
            if (person.UserId == scope.Value.UserId)
            {
                continue;
            }

            var access = await AuthorizePeopleAsync(scope.Value.TenantId, scope.Value.UserId, person.UserId, cancellationToken).ConfigureAwait(false);
            if (access.IsSuccess)
            {
                allowed.Add(person);
            }
        }

        return Result.Success<IReadOnlyList<ChatPerson>>(allowed);
    }

    public async Task<Result<ChatSummaryResponse>> SummaryAsync(CancellationToken cancellationToken)
    {
        var inbox = await ListInboxAsync(null, 50, null, "unread", cancellationToken).ConfigureAwait(false);
        if (inbox.IsFailure)
        {
            return Result.Failure<ChatSummaryResponse>(inbox.Error);
        }

        return Result.Success(new ChatSummaryResponse(inbox.Value.Count, inbox.Value.Sum(item => item.UnreadCount)));
    }

    public async Task<Result<IReadOnlyList<ChatRuleResponse>>> GetRulesAsync(CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatConfigure, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatRuleResponse>>(scope.Error);
        }

        await _chats.EnsureDefaultsAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var rules = await _chats.GetRulesAsync(scope.Value.TenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ChatRuleResponse>>(rules.Values.OrderBy(item => item.Code).Select(Map).ToArray());
    }

    public async Task<Result<IReadOnlyList<ChatRuleResponse>>> SaveRulesAsync(IReadOnlyList<ChatRuleResponse> rules, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatConfigure, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatRuleResponse>>(scope.Error);
        }

        if (rules.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != ChatRuleCodes.All.Length
            || rules.Any(item => !ChatRuleCodes.All.Contains(item.Code)))
        {
            return Result.Failure<IReadOnlyList<ChatRuleResponse>>(ChatErrors.InvalidMessage);
        }

        await _chats.SaveRulesAsync(scope.Value.TenantId, rules.Select(item => new ChatRuleState(item.Code, item.Enabled, item.Scope, item.AllowImages, item.AllowDocuments)).ToArray(), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetRulesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ChatAttachmentContent>> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChatAttachmentContent>(scope.Error);
        }

        var attachment = await _chats.FindAttachmentAsync(scope.Value.TenantId, attachmentId, cancellationToken).ConfigureAwait(false);
        if (attachment is null)
        {
            return Result.Failure<ChatAttachmentContent>(ChatErrors.NotFound);
        }

        var view = await RequireViewAsync(attachment.Value.ConversationId, PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return Result.Failure<ChatAttachmentContent>(view.Error);
        }

        var kind = AttachmentKind(attachment.Value.Attachment.ContentType);
        if (kind == ChatMessageType.Image && !view.Value.Access.AllowImages || kind == ChatMessageType.Document && !view.Value.Access.AllowDocuments)
        {
            return Result.Failure<ChatAttachmentContent>(ChatErrors.InvalidAttachment);
        }

        var file = await _files.DownloadAsync(new FileDownloadRequest(scope.Value.TenantId, attachment.Value.Attachment.StoredFileId), cancellationToken).ConfigureAwait(false);
        return file.IsFailure
            ? Result.Failure<ChatAttachmentContent>(file.Error)
            : Result.Success(new ChatAttachmentContent(file.Value.Content, file.Value.ContentType, file.Value.FileName));
    }

    public async Task<Result> JoinAsync(Guid conversationId, string connectionId, CancellationToken cancellationToken)
    {
        var view = await RequireViewAsync(conversationId, PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (view.IsFailure)
        {
            return view;
        }

        _presence.Watch(view.Value.Sender.UserId, conversationId, connectionId);
        return Result.Success();
    }

    private static ChatRuleResponse Map(ChatRuleState rule) => new(rule.Code, rule.Enabled, rule.Scope, rule.AllowImages, rule.AllowDocuments);

    private async Task<Result<(Guid TenantId, Guid UserId)>> GateAsync(string permission, CancellationToken cancellationToken)
    {
        var gate = await _guard.AllowAsync(permission, FeatureCodes.GymChat, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure || _actor.TenantId is not Guid tenantId || _actor.UserId is not Guid userId)
        {
            return Result.Failure<(Guid, Guid)>(gate.IsFailure ? gate.Error : IdentityErrors.InvalidCredentials);
        }

        return Result.Success((tenantId, userId));
    }

    private async Task<Result<(ChatPerson Sender, ChatPerson Recipient, ChatAccessDecision Access)>> AuthorizePeopleAsync(Guid tenantId, Guid senderUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var sender = await _chats.FindPersonAsync(tenantId, senderUserId, cancellationToken).ConfigureAwait(false);
        var recipient = await _chats.FindPersonAsync(tenantId, recipientUserId, cancellationToken).ConfigureAwait(false);
        if (sender is null || recipient is null || sender.UserId == recipient.UserId)
        {
            return Result.Failure<(ChatPerson, ChatPerson, ChatAccessDecision)>(ChatErrors.Forbidden);
        }

        await _chats.EnsureDefaultsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var rules = await _chats.GetRulesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var assigned = ChatPermissionPolicy.SharesAssignment(sender.MemberProfileId, sender.AssignedTrainerProfileId, recipient.TrainerProfileId)
            || ChatPermissionPolicy.SharesAssignment(recipient.MemberProfileId, recipient.AssignedTrainerProfileId, sender.TrainerProfileId);
        var decision = ChatPermissionPolicy.Evaluate(sender.Role, recipient.Role, assigned, rules);
        return decision.Allowed
            ? Result.Success((sender, recipient, decision))
            : Result.Failure<(ChatPerson, ChatPerson, ChatAccessDecision)>(ChatErrors.Forbidden);
    }

    private async Task<Result<ChatView>> RequireViewAsync(Guid conversationId, string permission, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(permission, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChatView>(scope.Error);
        }

        var participant = await _chats.GetParticipantAsync(scope.Value.TenantId, conversationId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        var conversation = await _chats.GetConversationAsync(scope.Value.TenantId, conversationId, cancellationToken).ConfigureAwait(false);
        var other = await _chats.OtherPersonAsync(scope.Value.TenantId, conversationId, scope.Value.UserId, cancellationToken).ConfigureAwait(false);
        if (participant is null || conversation is null || other is null)
        {
            return Result.Failure<ChatView>(ChatErrors.NotFound);
        }

        var access = await AuthorizePeopleAsync(scope.Value.TenantId, scope.Value.UserId, other.UserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<ChatView>(access.Error);
        }

        return Result.Success(new ChatView(scope.Value.TenantId, conversation, access.Value.Sender, access.Value.Recipient, access.Value.Access));
    }
}
