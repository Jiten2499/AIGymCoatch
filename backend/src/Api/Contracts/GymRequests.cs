namespace LifeFitAI.Api.Contracts;

public sealed record UpdateBusinessProfileRequest(
    string? LegalName,
    string? Description,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? LogoUrl,
    string TimeZone,
    string Currency);

public sealed record CreateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string TimeZone);

public sealed record UpdateBranchRequest(
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string TimeZone);

public sealed record CreateMemberRequest(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    Guid BranchId,
    Guid PlanId,
    DateOnly StartDate,
    bool AutoRenew);

public sealed record UpdateMemberRequest(
    DateOnly? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    LifeFitAI.Domain.Gym.ProfileStatus Status);

public sealed record AddStaffRequest(
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? Phone,
    LifeFitAI.Domain.Gym.StaffType StaffType,
    Guid? BranchId);

public sealed record UpdateStaffRequest(Guid? BranchId, LifeFitAI.Domain.Gym.StaffType StaffType, LifeFitAI.Domain.Gym.ProfileStatus Status);

public sealed record AddCoachRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Specialization,
    Guid? BranchId);

public sealed record UpdateTrainerRequest(
    string? Bio,
    string? Specialization,
    int? ExperienceYears,
    string? Certification,
    string? ProfileImageUrl,
    Guid? BranchId,
    LifeFitAI.Domain.Gym.ProfileStatus Status,
    string? FirstName = null,
    string? LastName = null);

public sealed record PlanRequest(
    string Name,
    string? Description,
    int DurationDays,
    LifeFitAI.Domain.Gym.MembershipType MembershipType,
    int? MaxVisits,
    decimal Price,
    string Currency);

public sealed record CreateMembershipRequest(Guid MemberProfileId, Guid BranchId, Guid PlanId, DateOnly StartDate, bool AutoRenew, string? Notes);

public sealed record ChangeMembershipRequest(string? Reason, bool AutoRenew);

public sealed record CheckInRequest(Guid? MemberProfileId, Guid BranchId, LifeFitAI.Domain.Gym.CheckInMethod Method, string? QrToken);

public sealed record CheckOutRequest(Guid AttendanceId);

public sealed record IssueQrTokenRequest(Guid MemberProfileId);
