using LifeFitAI.Application.Chat;
using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class ChatRepository : IChatRepository
{
    private readonly ApplicationDbContext _db;

    public ChatRepository(ApplicationDbContext db) => _db = db;

    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.ChatPermissionRules.Where(item => item.TenantId == tenantId).Select(item => item.RuleCode).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in ChatPermissionPolicy.Defaults().Where(item => !existing.Contains(item.Code)))
        {
            _db.ChatPermissionRules.Add(ChatPermissionRule.Create(Guid.NewGuid(), tenantId, rule.Code, rule.Enabled, rule.Scope, rule.AllowImages, rule.AllowDocuments));
        }
    }

    public async Task<IReadOnlyDictionary<string, ChatRuleState>> GetRulesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _db.ChatPermissionRules.AsNoTracking().Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var map = ChatPermissionPolicy.Defaults().ToDictionary(item => item.Code, item => item);
        foreach (var row in rows)
        {
            map[row.RuleCode] = new ChatRuleState(row.RuleCode, row.Enabled, row.Scope, row.AllowImages, row.AllowDocuments);
        }

        return map;
    }

    public async Task SaveRulesAsync(Guid tenantId, IReadOnlyList<ChatRuleState> rules, CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var rows = await _db.ChatPermissionRules.Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rule in rules)
        {
            rows.FirstOrDefault(item => item.RuleCode == rule.Code)?.Update(rule.Enabled, rule.Scope, rule.AllowImages, rule.AllowDocuments);
        }
    }

    public Task<ChatPerson?> FindPersonAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        MapPersonAsync(tenantId, userId, cancellationToken);

    public async Task<IReadOnlyList<ChatPerson>> SearchPeopleAsync(Guid tenantId, string? term, int take, CancellationToken cancellationToken)
    {
        var ids = await DirectoryIdsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var users = _db.Users.AsNoTracking().Where(user => ids.Contains(user.Id) && user.Status == UserStatus.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();
            users = users.Where(user => user.FirstName.Contains(value) || user.LastName.Contains(value));
        }

        var page = await users.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).Select(user => user.Id).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        var people = new List<ChatPerson>();
        foreach (var id in page)
        {
            var person = await MapPersonAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
            if (person is not null)
            {
                people.Add(person);
            }
        }

        return people;
    }

    public async Task<ChatConversation> GetOrCreateDirectAsync(Guid tenantId, Guid leftUserId, Guid rightUserId, CancellationToken cancellationToken)
    {
        var key = ChatConversation.DirectPairKey(leftUserId, rightUserId);
        var existing = await _db.ChatConversations.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.PairKey == key, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var conversation = ChatConversation.CreateDirect(Guid.NewGuid(), tenantId, leftUserId, rightUserId);
        _db.ChatConversations.Add(conversation);
        _db.ChatParticipants.Add(ChatParticipant.Create(Guid.NewGuid(), tenantId, conversation.Id, leftUserId, DateTime.UtcNow));
        _db.ChatParticipants.Add(ChatParticipant.Create(Guid.NewGuid(), tenantId, conversation.Id, rightUserId, DateTime.UtcNow));
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return conversation;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            foreach (var entry in _db.ChangeTracker.Entries().Where(item => item.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return await _db.ChatConversations.FirstAsync(item => item.TenantId == tenantId && item.PairKey == key, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<ChatParticipant?> GetParticipantAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken cancellationToken) =>
        _db.ChatParticipants.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ConversationId == conversationId && item.UserId == userId, cancellationToken);

    public async Task<ChatPerson?> OtherPersonAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var otherId = await _db.ChatParticipants.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ConversationId == conversationId && item.UserId != userId)
            .Select(item => item.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return otherId == Guid.Empty ? null : await MapPersonAsync(tenantId, otherId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ChatInboxItem>> ListInboxAsync(Guid tenantId, Guid userId, DateTime? before, int take, string? term, CancellationToken cancellationToken)
    {
        var rows = _db.ChatParticipants.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId);
        var conversations = _db.ChatConversations.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (before is DateTime cursor)
        {
            conversations = conversations.Where(item => item.LastMessageAt == null || item.LastMessageAt < cursor);
        }

        var page = await (
            from self in rows
            join conversation in conversations on self.ConversationId equals conversation.Id
            join other in _db.ChatParticipants.IgnoreQueryFilters().AsNoTracking() on conversation.Id equals other.ConversationId
            where other.TenantId == tenantId && other.UserId != userId
            orderby conversation.LastMessageAt descending
            select new { conversation.Id, conversation.LastPreview, conversation.LastMessageAt, other.UserId, self.LastReadAt })
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<ChatInboxItem>();
        foreach (var row in page)
        {
            var person = await MapPersonAsync(tenantId, row.UserId, cancellationToken).ConfigureAwait(false);
            if (person is null || (term is not null && !person.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var unread = await _db.ChatMessages.CountAsync(item => item.ConversationId == row.Id && item.SenderUserId != userId && (row.LastReadAt == null || item.CreatedAt > row.LastReadAt), cancellationToken).ConfigureAwait(false);
            items.Add(new ChatInboxItem(row.Id, person.UserId, person.DisplayName, person.Role, row.LastPreview, row.LastMessageAt, unread, person.HasPhoto));
        }

        return items;
    }

    public Task<ChatMessage?> FindClientMessageAsync(Guid tenantId, Guid senderUserId, string clientMessageId, CancellationToken cancellationToken) =>
        _db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.SenderUserId == senderUserId && item.ClientMessageId == clientMessageId, cancellationToken);

    public Task AddMessageAsync(ChatConversation conversation, ChatMessage message, ChatAttachment? attachment, CancellationToken cancellationToken)
    {
        _db.ChatMessages.Add(message);
        if (attachment is not null)
        {
            _db.ChatAttachments.Add(attachment);
        }

        return Task.CompletedTask;
    }

    public Task<ChatConversation?> GetConversationAsync(Guid tenantId, Guid conversationId, CancellationToken cancellationToken) =>
        _db.ChatConversations.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == conversationId, cancellationToken);

    public async Task<IReadOnlyList<ChatMessageItem>> ListMessagesAsync(Guid tenantId, Guid conversationId, Guid readerUserId, DateTime? before, Guid? beforeId, int take, CancellationToken cancellationToken)
    {
        var query = _db.ChatMessages.AsNoTracking().Where(item => item.TenantId == tenantId && item.ConversationId == conversationId);
        if (before is DateTime cursor)
        {
            query = query.Where(item => item.CreatedAt < cursor || (item.CreatedAt == cursor && beforeId != null && item.Id != beforeId));
        }

        var messages = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = messages.Select(item => item.Id).ToArray();
        var attachments = await _db.ChatAttachments.AsNoTracking().Where(item => ids.Contains(item.MessageId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var otherRead = await _db.ChatParticipants.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ConversationId == conversationId && item.UserId != readerUserId)
            .Select(item => item.LastReadAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return messages.Select(item =>
        {
            var file = attachments.FirstOrDefault(attachment => attachment.MessageId == item.Id);
            var read = otherRead is DateTime stamp && stamp >= item.CreatedAt;
            return new ChatMessageItem(item.Id, item.ConversationId, item.SenderUserId, item.MessageType, item.Body, item.CreatedAt, read, file?.Id, file?.FileName, file?.ContentType, file?.SizeBytes);
        }).ToArray();
    }

    public async Task MarkReadAsync(Guid tenantId, Guid conversationId, Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var participant = await _db.ChatParticipants.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ConversationId == conversationId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        var message = await _db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ConversationId == conversationId && item.Id == messageId, cancellationToken).ConfigureAwait(false);
        if (participant is null || message is null)
        {
            return;
        }

        participant.MarkRead(message.Id, message.CreatedAt);
    }

    public async Task<ChatSummaryResponse> SummarizeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var inbox = await ListInboxAsync(tenantId, userId, null, 50, null, cancellationToken).ConfigureAwait(false);
        return new ChatSummaryResponse(inbox.Count(item => item.UnreadCount > 0), inbox.Sum(item => item.UnreadCount));
    }

    public async Task<(ChatAttachment Attachment, Guid ConversationId)?> FindAttachmentAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var row = await (
            from attachment in _db.ChatAttachments.AsNoTracking()
            join message in _db.ChatMessages.AsNoTracking() on attachment.MessageId equals message.Id
            where attachment.TenantId == tenantId && attachment.Id == attachmentId
            select new { attachment, message.ConversationId }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : (row.attachment, row.ConversationId);
    }

    public Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken) =>
        _db.Notifications.AddAsync(notification, cancellationToken).AsTask();

    public Task AddOutboxAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    private async Task<List<Guid>> DirectoryIdsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var members = await _db.TenantMembers.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == MembershipStatus.Active)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var staff = await _db.StaffProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == ProfileStatus.Active)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var trainers = await _db.TrainerProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == ProfileStatus.Active)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var gymMembers = await _db.MemberProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId != null && item.Status == ProfileStatus.Active)
            .Select(item => item.UserId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return members.Concat(staff).Concat(trainers).Concat(gymMembers).Distinct().ToList();
    }

    private async Task<ChatPerson?> MapPersonAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId && item.Status == UserStatus.Active, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var roles = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.TenantId == tenantId && assignment.UserId == userId
            select role.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        var member = await _db.MemberProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.DeletedAt == null ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var trainer = await _db.TrainerProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.DeletedAt == null ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var staff = await _db.StaffProfiles.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        Guid? assigned = member?.AssignedTrainerId;
        if (member is not null && assigned is null)
        {
            assigned = await _db.TrainerMemberAssignments.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.MemberProfileId == member.Id && item.Status == TrainerAssignmentStatus.Active)
                .Select(item => (Guid?)item.TrainerProfileId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var party = ChatPermissionPolicy.ResolveParty(roles, staff, trainer is not null, member is not null);
        var historicalMembership = await _db.TenantMembers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (party is null && historicalMembership)
        {
            party = ChatPartyRole.Staff;
        }

        if (party is null)
        {
            return null;
        }

        var inGym = await _db.TenantMembers.AsNoTracking().AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Status == MembershipStatus.Active, cancellationToken).ConfigureAwait(false)
            || historicalMembership || staff || trainer is not null || member is not null;
        return inGym ? new ChatPerson(userId, $"{user.FirstName} {user.LastName}".Trim(), party.Value, member?.Id, trainer?.Id, assigned, Portrait(tenantId, user, member, trainer, party.Value) is not null) : null;
    }
}
