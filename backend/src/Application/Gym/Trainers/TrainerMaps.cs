using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym;

public static class TrainerMaps
{
    public static TrainerResponse ToResponse(TrainerProfile trainer, UserAccount user, int assignedMembers = 0) =>
        new(
            trainer.Id,
            trainer.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            trainer.Bio,
            trainer.Specialization,
            trainer.ExperienceYears,
            trainer.Certification,
            trainer.ProfileImageUrl,
            trainer.BranchId,
            trainer.Status,
            assignedMembers,
            trainer.EmployeeCode,
            user.Phone,
            trainer.JoiningDate,
            trainer.CommissionEligible,
            trainer.PhotoStorageKey is not null);
}
