using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Members;

public static class MemberPtOpening
{
    public static async Task<Guid> OpenAsync(IPtPackageRepository packages, Guid tenantId, Guid memberProfileId, Guid? trainerProfileId, Guid packageId, DateOnly start, CancellationToken cancellationToken)
    {
        var package = await packages.GetAsync(tenantId, packageId, cancellationToken).ConfigureAwait(false);
        if (package is null)
        {
            throw new MemberOnboardingException(GymErrors.NotFound);
        }

        if (package.Status != PtPackageStatus.Active)
        {
            throw new MemberOnboardingException(GymErrors.InvalidState);
        }

        if (await packages.HasActiveAssignmentAsync(tenantId, memberProfileId, package.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new MemberOnboardingException(GymErrors.Conflict);
        }

        var assignmentId = Guid.NewGuid();
        await packages.AddAssignmentAsync(MemberPtAssignment.Open(assignmentId, package, memberProfileId, trainerProfileId, start), cancellationToken).ConfigureAwait(false);
        return assignmentId;
    }
}
