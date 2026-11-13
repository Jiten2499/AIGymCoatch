using LifeFitAI.Application.Gym.Pt;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Members;

public static class MemberPtOpening
{
    public static async Task<MemberPtAssignment> OpenAsync(IPtPackageRepository packages, Guid tenantId, Guid memberProfileId, Guid? trainerProfileId, Guid packageId, DateOnly start, CancellationToken cancellationToken, PtOpeningPayment? payment = null)
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
        var assignment = MemberPtAssignment.Open(assignmentId, package, memberProfileId, trainerProfileId, start);
        if (payment is not null)
        {
            try
            {
                assignment.RecordOpeningPayment(payment.Amount, payment.Method, payment.Date, payment.ReferenceNumber, payment.Notes);
            }
            catch (ArgumentException)
            {
                throw new MemberOnboardingException(GymErrors.InvalidState);
            }
        }

        await packages.AddAssignmentAsync(assignment, cancellationToken).ConfigureAwait(false);
        return assignment;
    }
}

public sealed record PtOpeningPayment(decimal Amount, MembershipPaymentMethod Method, DateOnly Date, string? ReferenceNumber, string? Notes);
