using LifeFitAI.Domain.Identity;

namespace LifeFitAI.Application.Identity;

public sealed record UserProfile(Guid Id, string Email, string FirstName, string LastName, bool HasPhoto = false);

public static class UserProfiles
{
    public static UserProfile From(UserAccount user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.PhotoStorageKey is not null);
}

public sealed record TenantSummary(Guid Id, string Name, TenantType Type, TenantStatus Status);

public sealed record WorkspaceSummary(
    Guid Id,
    string Name,
    TenantType Type,
    TenantStatus Status,
    IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserProfile User,
    TenantSummary CurrentTenant);

public sealed record LicenseSummary(
    Guid Id,
    string PlanCode,
    string Status,
    DateTime StartsAt,
    DateTime? ExpiresAt,
    bool IsUsable);

public sealed record FeatureSummary(string FeatureCode, bool Enabled);
