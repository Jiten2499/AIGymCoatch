using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym;

public static class TrainerMaps
{
    public static TrainerResponse ToResponse(
        TrainerProfile trainer,
        UserAccount user,
        int assignedMembers = 0,
        StaffProfile? staff = null,
        Guid? subscriptionId = null,
        string? planName = null) =>
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
            trainer.BranchId ?? staff?.BranchId,
            trainer.Status,
            assignedMembers,
            trainer.EmployeeCode ?? staff?.EmployeeCode,
            user.Phone,
            trainer.JoiningDate ?? staff?.JoiningDate,
            trainer.CommissionEligible,
            trainer.PhotoStorageKey is not null,
            staff?.Id,
            staff?.Designation,
            staff?.DateOfBirth,
            staff?.Gender,
            staff?.Address,
            staff?.EmergencyContactName,
            staff?.EmergencyContactPhone,
            staff?.EmploymentType,
            staff?.ReportingManagerUserId,
            staff?.EmploymentNotes,
            subscriptionId,
            planName);
}
