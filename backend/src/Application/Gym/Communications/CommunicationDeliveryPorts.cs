using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Communications;

public sealed record AudiencePreview(int Members, int Trainers, int Staff, int BusinessUsers, int Total);

public interface ICommunicationDelivery
{
    Task<AudiencePreview> PreviewAsync(
        Guid tenantId,
        CommunicationAudienceType audienceType,
        Guid? packageId,
        Guid? trainerId,
        int? expiringWithinDays,
        IReadOnlyList<Guid> selectedMemberIds,
        IReadOnlyList<Guid> selectedTrainerIds,
        IReadOnlyList<Guid> selectedStaffIds,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task DeliverAsync(Communication communication, DateTime utcNow, CancellationToken cancellationToken);

    Task<int> PublishDueAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task<int> DeliverPublishedWithoutRecipientsAsync(DateTime utcNow, CancellationToken cancellationToken);
}
