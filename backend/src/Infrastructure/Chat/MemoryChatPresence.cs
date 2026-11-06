using System.Collections.Concurrent;
using LifeFitAI.Application.Chat;

namespace LifeFitAI.Infrastructure.Chat;

public sealed class MemoryChatPresence : IChatPresence
{
    private readonly ConcurrentDictionary<string, (Guid UserId, Guid? ConversationId)> _connections = new();

    public void Connected(Guid userId, string connectionId) => _connections[connectionId] = (userId, null);

    public void Disconnected(string connectionId) => _connections.TryRemove(connectionId, out _);

    public void Watch(Guid userId, Guid conversationId, string connectionId) => _connections[connectionId] = (userId, conversationId);

    public void Unwatch(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var current))
        {
            _connections[connectionId] = (current.UserId, null);
        }
    }

    public bool IsWatching(Guid userId, Guid conversationId) =>
        _connections.Values.Any(item => item.UserId == userId && item.ConversationId == conversationId);
}
