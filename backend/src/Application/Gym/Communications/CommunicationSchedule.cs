using LifeFitAI.Application.Gym.Expenses;
using LifeFitAI.Domain.Platform;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record CommunicationScheduleTimes(DateTime? PublishAtUtc, DateTime? ExpiresAtUtc, string TimeZoneId);

public static class CommunicationScheduleRules
{
    public static Result<CommunicationScheduleTimes> Normalize(DateTime? publishAt, DateTime? expiresAt, bool scheduling, DateTime utcNow, string? timeZoneId)
    {
        var zone = ExpenseCalendar.Zone(timeZoneId);
        var publishUtc = publishAt is DateTime start ? ToUtc(start, zone) : (DateTime?)null;
        var expiresUtc = expiresAt is DateTime end ? ToUtc(end, zone) : (DateTime?)null;
        if (expiresUtc is DateTime expiry && publishUtc is DateTime publish && expiry <= publish)
        {
            return Result.Failure<CommunicationScheduleTimes>(GymErrors.InvalidState);
        }

        if (scheduling && (publishUtc is null || publishUtc <= utcNow))
        {
            return Result.Failure<CommunicationScheduleTimes>(GymErrors.InvalidState);
        }

        return Result.Success(new CommunicationScheduleTimes(publishUtc, expiresUtc, zone.Id));
    }

    public static DateTime ToUtc(DateTime wallClock, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), zone);
}

public interface ICommunicationSchedule
{
    Task<Result<CommunicationScheduleTimes>> NormalizeAsync(Guid tenantId, DateTime? publishAt, DateTime? expiresAt, bool scheduling, CancellationToken cancellationToken);

    Task SyncAsync(Domain.Gym.Communication communication, CancellationToken cancellationToken);
}

public interface IOutboxStore
{
    Task<OutboxMessage?> FindAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken);

    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(int take, CancellationToken cancellationToken);
}
