using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Gym.Members;

public static class MemberMaps
{
    public static MemberResponse From(MemberProfile profile, UserAccount? user, string? trainerName = null)
    {
        var firstName = profile.FirstName.Length > 0 ? profile.FirstName : user?.FirstName ?? string.Empty;
        var lastName = profile.LastName.Length > 0 ? profile.LastName : user?.LastName ?? string.Empty;
        return new(
            profile.Id,
            profile.UserId,
            profile.MemberCode,
            user?.Email ?? string.Empty,
            firstName,
            lastName,
            profile.Phone ?? user?.Phone,
            profile.DateOfBirth,
            profile.Gender,
            profile.EmergencyContactName,
            profile.EmergencyContactPhone,
            profile.Address,
            profile.City,
            profile.State,
            profile.PostalCode,
            profile.JoinDate,
            profile.Status,
            profile.EmergencyContactRelationship,
            profile.FitnessGoal,
            profile.AssignedTrainerId,
            trainerName,
            profile.PhotoStorageKey is not null);
    }

    public static bool TryGoal(string? value, out FitnessGoalType? goal)
    {
        goal = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse<FitnessGoalType>(value, ignoreCase: false, out var parsed))
        {
            return false;
        }

        goal = parsed;
        return true;
    }
}

public static class MemberFieldRules
{
    public static readonly string[] Genders = ["Female", "Male", "NonBinary", "PreferNotToSay"];

    public const int MaxPhotoBytes = 2 * 1024 * 1024;

    public static bool Phone(string? phone) =>
        phone is null || System.Text.RegularExpressions.Regex.IsMatch(phone, "^\\+?[0-9][0-9 \\-]{6,30}$");

    public static bool Gender(string? gender) => gender is null || Genders.Contains(gender);

    public static string PhotoKey(Guid tenantId, Guid memberId, Guid photoId) =>
        $"tenants/{tenantId:D}/members/{memberId:D}/profile/{photoId:D}";

    public static bool OwnsPhoto(Guid tenantId, Guid memberId, string? key) =>
        key is not null && key.StartsWith($"tenants/{tenantId:D}/members/{memberId:D}/profile/", StringComparison.Ordinal);

    public static bool TryContentType(ReadOnlySpan<byte> content, string? declared, out string contentType)
    {
        contentType = string.Empty;
        if (content.Length is < 12 or > MaxPhotoBytes)
        {
            return false;
        }

        if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            contentType = "image/jpeg";
        }
        else if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
        {
            contentType = "image/png";
        }
        else if (content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            contentType = "image/webp";
        }
        else
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(declared) || string.Equals(declared, contentType, StringComparison.OrdinalIgnoreCase);
    }
}
