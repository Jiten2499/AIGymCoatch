using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Configuration;

public sealed record MembershipConfigDto(bool AllowOverlappingMemberships, bool AllowEarlyRenewal, bool DefaultStartOnJoinDate, int GracePeriodDays, bool AutoExpireEnabled);

public sealed record FreezeConfigDto(int MaxFreezeDays, int MinFreezeDays, int MaxFreezeOccurrences, bool FreezeRequiresApproval, int MaxExtensionDays, bool ExtensionRequiresApproval);

public sealed record AttendanceConfigDto(int DuplicateCheckInMinutes, string? OpeningTime, string? ClosingTime, int CheckInGraceMinutes, CheckInMethod DefaultCheckInMethod);

public sealed record CrmConfigDto(int DefaultTrialDays, int FollowUpReminderHours, int DefaultFollowUpDays);

public sealed record MemberCategoryItem(Guid Id, string Name, string? Description, bool IsActive);

public sealed record DiscountRuleItem(
    Guid Id,
    string Name,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    DiscountAppliesTo AppliesTo,
    Guid? PackageId,
    string? PackageName,
    PackageCategory? PackageCategory,
    Guid? MemberCategoryId,
    string? MemberCategoryName,
    decimal? MinimumPackagePrice,
    int? MinimumDurationDays,
    decimal? MaximumDiscountAmount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive);

public sealed record CrmCatalogItem(Guid Id, string Name, string? Code, bool IsSystem, bool IsActive);

public sealed record BusinessConfigurationResponse(
    MembershipConfigDto Membership,
    FreezeConfigDto Freeze,
    AttendanceConfigDto Attendance,
    CrmConfigDto Crm,
    IReadOnlyList<MemberCategoryItem> MemberCategories,
    IReadOnlyList<DiscountRuleItem> Discounts,
    IReadOnlyList<CrmCatalogItem> LeadSources,
    IReadOnlyList<CrmCatalogItem> LostReasons);
