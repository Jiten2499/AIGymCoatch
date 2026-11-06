using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2,
}

public enum DiscountAppliesTo
{
    AllPackages = 1,
    SpecificPackage = 2,
    PackageCategory = 3,
    MemberCategory = 4,
}

public sealed class TenantBusinessConfig : BaseEntity
{
    private TenantBusinessConfig()
    {
    }

    public Guid TenantId { get; private set; }

    public bool AllowOverlappingMemberships { get; private set; }

    public bool AllowEarlyRenewal { get; private set; }

    public bool DefaultStartOnJoinDate { get; private set; }

    public int GracePeriodDays { get; private set; }

    public bool AutoExpireEnabled { get; private set; }

    public int MaxFreezeDays { get; private set; }

    public int MinFreezeDays { get; private set; }

    public int MaxFreezeOccurrences { get; private set; }

    public bool FreezeRequiresApproval { get; private set; }

    public int MaxExtensionDays { get; private set; }

    public bool ExtensionRequiresApproval { get; private set; }

    public int DuplicateCheckInMinutes { get; private set; }

    public TimeOnly? OpeningTime { get; private set; }

    public TimeOnly? ClosingTime { get; private set; }

    public int CheckInGraceMinutes { get; private set; }

    public CheckInMethod DefaultCheckInMethod { get; private set; }

    public int DefaultTrialDays { get; private set; }

    public int FollowUpReminderHours { get; private set; }

    public int DefaultFollowUpDays { get; private set; }

    public static TenantBusinessConfig CreateDefault(Guid id, Guid tenantId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            AllowOverlappingMemberships = false,
            AllowEarlyRenewal = true,
            DefaultStartOnJoinDate = true,
            GracePeriodDays = 0,
            AutoExpireEnabled = true,
            MaxFreezeDays = 30,
            MinFreezeDays = 1,
            MaxFreezeOccurrences = 2,
            FreezeRequiresApproval = false,
            MaxExtensionDays = 14,
            ExtensionRequiresApproval = false,
            DuplicateCheckInMinutes = 30,
            OpeningTime = new TimeOnly(6, 0),
            ClosingTime = new TimeOnly(22, 0),
            CheckInGraceMinutes = 15,
            DefaultCheckInMethod = CheckInMethod.Manual,
            DefaultTrialDays = 3,
            FollowUpReminderHours = 24,
            DefaultFollowUpDays = 2,
        };

    public void UpdateMembership(bool allowOverlapping, bool allowEarlyRenewal, bool defaultStartOnJoinDate, int gracePeriodDays, bool autoExpireEnabled)
    {
        AllowOverlappingMemberships = allowOverlapping;
        AllowEarlyRenewal = allowEarlyRenewal;
        DefaultStartOnJoinDate = defaultStartOnJoinDate;
        GracePeriodDays = gracePeriodDays;
        AutoExpireEnabled = autoExpireEnabled;
    }

    public void UpdateFreeze(int maxFreezeDays, int minFreezeDays, int maxFreezeOccurrences, bool freezeRequiresApproval, int maxExtensionDays, bool extensionRequiresApproval)
    {
        MaxFreezeDays = maxFreezeDays;
        MinFreezeDays = minFreezeDays;
        MaxFreezeOccurrences = maxFreezeOccurrences;
        FreezeRequiresApproval = freezeRequiresApproval;
        MaxExtensionDays = maxExtensionDays;
        ExtensionRequiresApproval = extensionRequiresApproval;
    }

    public void UpdateAttendance(int duplicateCheckInMinutes, TimeOnly? openingTime, TimeOnly? closingTime, int checkInGraceMinutes, CheckInMethod defaultCheckInMethod)
    {
        DuplicateCheckInMinutes = duplicateCheckInMinutes;
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        CheckInGraceMinutes = checkInGraceMinutes;
        DefaultCheckInMethod = defaultCheckInMethod;
    }

    public void UpdateCrm(int defaultTrialDays, int followUpReminderHours, int defaultFollowUpDays)
    {
        DefaultTrialDays = defaultTrialDays;
        FollowUpReminderHours = followUpReminderHours;
        DefaultFollowUpDays = defaultFollowUpDays;
    }
}

public sealed class MemberCategory : BaseEntity
{
    private MemberCategory()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static MemberCategory Create(Guid id, Guid tenantId, string name, string? description) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
        };

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

public sealed class DiscountRule : BaseEntity
{
    private DiscountRule()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DiscountType DiscountType { get; private set; }

    public decimal Value { get; private set; }

    public DiscountAppliesTo AppliesTo { get; private set; }

    public Guid? PackageId { get; private set; }

    public PackageCategory? PackageCategory { get; private set; }

    public Guid? MemberCategoryId { get; private set; }

    public decimal? MinimumPackagePrice { get; private set; }

    public int? MinimumDurationDays { get; private set; }

    public decimal? MaximumDiscountAmount { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public static DiscountRule Create(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        DiscountType discountType,
        decimal value,
        DiscountAppliesTo appliesTo,
        Guid? packageId,
        PackageCategory? packageCategory,
        Guid? memberCategoryId,
        decimal? minimumPackagePrice,
        int? minimumDurationDays,
        decimal? maximumDiscountAmount,
        DateOnly? startDate,
        DateOnly? endDate) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DiscountType = discountType,
            Value = value,
            AppliesTo = appliesTo,
            PackageId = appliesTo == DiscountAppliesTo.SpecificPackage ? packageId : null,
            PackageCategory = appliesTo == DiscountAppliesTo.PackageCategory ? packageCategory : null,
            MemberCategoryId = appliesTo == DiscountAppliesTo.MemberCategory ? memberCategoryId : null,
            MinimumPackagePrice = minimumPackagePrice,
            MinimumDurationDays = minimumDurationDays,
            MaximumDiscountAmount = maximumDiscountAmount,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
        };

    public void Update(
        string name,
        string? description,
        DiscountType discountType,
        decimal value,
        DiscountAppliesTo appliesTo,
        Guid? packageId,
        PackageCategory? packageCategory,
        Guid? memberCategoryId,
        decimal? minimumPackagePrice,
        int? minimumDurationDays,
        decimal? maximumDiscountAmount,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DiscountType = discountType;
        Value = value;
        AppliesTo = appliesTo;
        PackageId = appliesTo == DiscountAppliesTo.SpecificPackage ? packageId : null;
        PackageCategory = appliesTo == DiscountAppliesTo.PackageCategory ? packageCategory : null;
        MemberCategoryId = appliesTo == DiscountAppliesTo.MemberCategory ? memberCategoryId : null;
        MinimumPackagePrice = minimumPackagePrice;
        MinimumDurationDays = minimumDurationDays;
        MaximumDiscountAmount = maximumDiscountAmount;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

public sealed class CrmLeadSource : BaseEntity
{
    private CrmLeadSource()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public static CrmLeadSource Create(Guid id, Guid tenantId, string name, string? code, bool isSystem) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            IsSystem = isSystem,
            IsActive = true,
        };

    public void Update(string name)
    {
        if (!IsSystem)
        {
            Name = name.Trim();
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

public sealed class CrmLostReason : BaseEntity
{
    private CrmLostReason()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public static CrmLostReason Create(Guid id, Guid tenantId, string name, string? code, bool isSystem) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = name.Trim(),
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            IsSystem = isSystem,
            IsActive = true,
        };

    public void Update(string name)
    {
        if (!IsSystem)
        {
            Name = name.Trim();
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
