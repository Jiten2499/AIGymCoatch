using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym.Members;

internal static class MemberMaps
{
    public static MemberResponse From(MemberProfile profile, UserAccount user) =>
        new(
            profile.Id,
            profile.UserId,
            profile.MemberCode,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            profile.DateOfBirth,
            profile.Gender,
            profile.EmergencyContactName,
            profile.EmergencyContactPhone,
            profile.Address,
            profile.City,
            profile.State,
            profile.PostalCode,
            profile.JoinDate,
            profile.Status);
}
