using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Communications;

public sealed partial class CommunicationCommandHandler
{
    private async Task StoreAudienceAsync(Guid tenantId, Guid communicationId, CommunicationAudienceType audienceType, IReadOnlyList<Guid>? selectedMemberIds, IReadOnlyList<Guid>? selectedTrainerIds, IReadOnlyList<Guid>? selectedStaffIds, CancellationToken cancellationToken)
    {
        if (audienceType == CommunicationAudienceType.SelectedMembers)
        {
            await _communications.ReplaceAudienceAsync(tenantId, communicationId, selectedMemberIds ?? [], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _communications.SoftDeleteAudienceAsync(tenantId, communicationId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }

        if (audienceType == CommunicationAudienceType.SelectedTrainers)
        {
            await _communications.ReplaceTargetsAsync(tenantId, communicationId, CommunicationTargetKind.Trainer, selectedTrainerIds ?? [], cancellationToken).ConfigureAwait(false);
        }
        else if (audienceType == CommunicationAudienceType.SelectedStaff)
        {
            await _communications.ReplaceTargetsAsync(tenantId, communicationId, CommunicationTargetKind.Staff, selectedStaffIds ?? [], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _communications.SoftDeleteTargetsAsync(tenantId, communicationId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result> ValidateAudienceAsync(
        Guid tenantId,
        CommunicationAudienceType audienceType,
        Guid? packageId,
        Guid? trainerId,
        IReadOnlyList<Guid>? selectedMemberIds,
        IReadOnlyList<Guid>? selectedTrainerIds,
        IReadOnlyList<Guid>? selectedStaffIds,
        CancellationToken cancellationToken)
    {
        if (audienceType == CommunicationAudienceType.MembershipPackage)
        {
            if (packageId is not Guid id || await _memberships.GetPlanAsync(tenantId, id, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(GymErrors.NotFound);
            }
        }

        if (audienceType == CommunicationAudienceType.TrainerMembers)
        {
            if (trainerId is not Guid id || await _people.GetTrainerAsync(tenantId, id, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(GymErrors.NotFound);
            }
        }

        if (audienceType == CommunicationAudienceType.SelectedMembers)
        {
            foreach (var memberId in selectedMemberIds ?? [])
            {
                if (await _people.GetMemberAsync(tenantId, memberId, cancellationToken).ConfigureAwait(false) is null)
                {
                    return Result.Failure(GymErrors.NotFound);
                }
            }
        }

        if (audienceType == CommunicationAudienceType.SelectedTrainers)
        {
            foreach (var id in selectedTrainerIds ?? [])
            {
                if (await _people.GetTrainerAsync(tenantId, id, cancellationToken).ConfigureAwait(false) is null)
                {
                    return Result.Failure(GymErrors.NotFound);
                }
            }
        }

        if (audienceType == CommunicationAudienceType.SelectedStaff)
        {
            foreach (var id in selectedStaffIds ?? [])
            {
                if (await _people.GetStaffAsync(tenantId, id, cancellationToken).ConfigureAwait(false) is null)
                {
                    return Result.Failure(GymErrors.NotFound);
                }
            }
        }

        return Result.Success();
    }
}
