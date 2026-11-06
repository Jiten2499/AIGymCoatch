using LifeFitAI.Application.Chat;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LifeFitAI.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ChatMessageService _chat;
    private readonly ICurrentActor _actor;
    private readonly IChatPresence _presence;
    private readonly ICircularAudience _circulars;

    public ChatHub(ChatMessageService chat, ICurrentActor actor, IChatPresence presence, ICircularAudience circulars)
    {
        _chat = chat;
        _actor = actor;
        _presence = presence;
        _circulars = circulars;
    }

    public static string ConversationGroup(Guid conversationId) => $"chat:{conversationId:D}";

    public static string UserGroup(Guid userId) => CircularGroups.User(userId);

    public override async Task OnConnectedAsync()
    {
        if (_actor.UserId is Guid userId)
        {
            _presence.Connected(userId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId)).ConfigureAwait(false);
            var operators = await _circulars.OperatorGroupAsync(Context.ConnectionAborted).ConfigureAwait(false);
            if (operators is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, operators).ConfigureAwait(false);
            }
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presence.Disconnected(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var joined = await _chat.JoinAsync(conversationId, Context.ConnectionId, Context.ConnectionAborted).ConfigureAwait(false);
        if (joined.IsFailure)
        {
            throw new HubException(joined.Error.Description);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId)).ConfigureAwait(false);
    }

    public Task LeaveConversation(Guid conversationId)
    {
        _presence.Unwatch(Context.ConnectionId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public async Task TypingStarted(Guid conversationId)
    {
        var joined = await _chat.JoinAsync(conversationId, Context.ConnectionId, Context.ConnectionAborted).ConfigureAwait(false);
        if (joined.IsFailure)
        {
            return;
        }

        await Clients.OthersInGroup(ConversationGroup(conversationId)).SendAsync("TypingStarted", new { conversationId, userId = _actor.UserId }).ConfigureAwait(false);
    }

    public Task TypingStopped(Guid conversationId) =>
        Clients.OthersInGroup(ConversationGroup(conversationId)).SendAsync("TypingStopped", new { conversationId, userId = _actor.UserId });
}

public sealed class SignalRChatNotifier : IChatNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRChatNotifier(IHubContext<ChatHub> hub) => _hub = hub;

    public Task MessageReceivedAsync(Guid conversationId, ChatMessageItem message, CancellationToken cancellationToken) =>
        _hub.Clients.Group(ChatHub.ConversationGroup(conversationId)).SendAsync("MessageReceived", message, cancellationToken);

    public Task MessageReadAsync(Guid conversationId, Guid userId, Guid messageId, DateTime readAt, CancellationToken cancellationToken) =>
        _hub.Clients.Group(ChatHub.ConversationGroup(conversationId)).SendAsync("MessageRead", new { conversationId, userId, messageId, readAt }, cancellationToken);

    public Task ConversationUpdatedAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        _hub.Clients.Group(ChatHub.UserGroup(userId)).SendAsync("ConversationUpdated", new { conversationId }, cancellationToken);
}
