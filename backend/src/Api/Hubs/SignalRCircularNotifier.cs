using LifeFitAI.Application.Gym.Communications;
using Microsoft.AspNetCore.SignalR;

namespace LifeFitAI.Api.Hubs;

public sealed class SignalRCircularNotifier : ICircularNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalRCircularNotifier(IHubContext<ChatHub> hub) => _hub = hub;

    public Task PublishedAsync(CircularPublishedNotice notice, IReadOnlyList<Guid> recipientUserIds, CancellationToken cancellationToken)
    {
        var groups = recipientUserIds.Select(ChatHub.UserGroup).Append(CircularGroups.Operators(notice.TenantId)).Distinct().ToArray();
        return _hub.Clients.Groups(groups).SendAsync(
            "CircularPublished",
            new
            {
                communicationId = notice.CommunicationId,
                tenantId = notice.TenantId,
                publishedAtUtc = notice.PublishedAtUtc,
                audience = notice.Audience,
            },
            cancellationToken);
    }
}
