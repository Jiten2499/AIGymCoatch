namespace LifeFitAI.Application.Gym.Communications;

public sealed record CircularPublishedNotice(Guid CommunicationId, Guid TenantId, DateTime PublishedAtUtc, string Audience);

public interface ICircularNotifier
{
    Task PublishedAsync(CircularPublishedNotice notice, IReadOnlyList<Guid> recipientUserIds, CancellationToken cancellationToken);
}

public interface ICircularAudience
{
    Task<string?> OperatorGroupAsync(CancellationToken cancellationToken);
}

public static class CircularPublication
{
    public const string MessageType = "Circular.Published";

    public static string Key(Guid communicationId) => $"{MessageType}:{communicationId:D}";
}

public static class CircularGroups
{
    public static string User(Guid userId) => $"user:{userId:D}";

    public static string Operators(Guid tenantId) => $"circulars:{tenantId:D}";
}

public sealed class NoopCircularNotifier : ICircularNotifier
{
    public Task PublishedAsync(CircularPublishedNotice notice, IReadOnlyList<Guid> recipientUserIds, CancellationToken cancellationToken) => Task.CompletedTask;
}
