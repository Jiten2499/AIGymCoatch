using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class BusinessConfigRepository : IBusinessConfigRepository
{
    private readonly ApplicationDbContext _db;

    public BusinessConfigRepository(ApplicationDbContext db) => _db = db;

    public Task<TenantBusinessConfig?> GetConfigAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.TenantBusinessConfigs.SingleOrDefaultAsync(config => config.TenantId == tenantId, cancellationToken);

    public async Task AddConfigAsync(TenantBusinessConfig config, CancellationToken cancellationToken) =>
        await _db.TenantBusinessConfigs.AddAsync(config, cancellationToken).ConfigureAwait(false);

    public Task<MemberCategory?> GetCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MemberCategories.SingleOrDefaultAsync(category => category.TenantId == tenantId && category.Id == id, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var value = name.Trim();
        return _db.MemberCategories.AnyAsync(category => category.TenantId == tenantId && category.Name == value && category.Id != excludeId, cancellationToken);
    }

    public async Task AddCategoryAsync(MemberCategory category, CancellationToken cancellationToken) =>
        await _db.MemberCategories.AddAsync(category, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MemberCategoryItem>> ListCategoriesAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _db.MemberCategories.AsNoTracking().Where(category => category.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query.OrderBy(category => category.Name)
            .Select(category => new MemberCategoryItem(category.Id, category.Name, category.Description, category.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> CategoryReferencedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken) =>
        _db.DiscountRules.AnyAsync(rule => rule.TenantId == tenantId && rule.MemberCategoryId == categoryId, cancellationToken);

    public Task<DiscountRule?> GetDiscountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.DiscountRules.SingleOrDefaultAsync(rule => rule.TenantId == tenantId && rule.Id == id, cancellationToken);

    public Task<bool> DiscountNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var value = name.Trim();
        return _db.DiscountRules.AnyAsync(rule => rule.TenantId == tenantId && rule.Name == value && rule.Id != excludeId, cancellationToken);
    }

    public async Task AddDiscountAsync(DiscountRule rule, CancellationToken cancellationToken) =>
        await _db.DiscountRules.AddAsync(rule, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DiscountRuleItem>> ListDiscountsAsync(Guid tenantId, bool? activeOnly, CancellationToken cancellationToken)
    {
        var query =
            from rule in _db.DiscountRules.AsNoTracking()
            join plan in _db.MembershipPlans.AsNoTracking() on rule.PackageId equals plan.Id into plans
            from plan in plans.DefaultIfEmpty()
            join category in _db.MemberCategories.AsNoTracking() on rule.MemberCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            where rule.TenantId == tenantId
            select new { rule, plan, category };
        if (activeOnly is true)
        {
            query = query.Where(row => row.rule.IsActive);
        }

        return await query.OrderBy(row => row.rule.Name)
            .Select(row => new DiscountRuleItem(
                row.rule.Id,
                row.rule.Name,
                row.rule.Description,
                row.rule.DiscountType,
                row.rule.Value,
                row.rule.AppliesTo,
                row.rule.PackageId,
                row.plan == null ? null : row.plan.Name,
                row.rule.PackageCategory,
                row.rule.MemberCategoryId,
                row.category == null ? null : row.category.Name,
                row.rule.MinimumPackagePrice,
                row.rule.MinimumDurationDays,
                row.rule.MaximumDiscountAmount,
                row.rule.StartDate,
                row.rule.EndDate,
                row.rule.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<CrmLeadSource?> GetLeadSourceAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.CrmLeadSources.SingleOrDefaultAsync(source => source.TenantId == tenantId && source.Id == id, cancellationToken);

    public Task<bool> LeadSourceNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var value = name.Trim();
        return _db.CrmLeadSources.AnyAsync(source => source.TenantId == tenantId && source.Name == value && source.Id != excludeId, cancellationToken);
    }

    public async Task AddLeadSourceAsync(CrmLeadSource source, CancellationToken cancellationToken) =>
        await _db.CrmLeadSources.AddAsync(source, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CrmCatalogItem>> ListLeadSourcesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.CrmLeadSources.AsNoTracking()
            .Where(source => source.TenantId == tenantId)
            .OrderBy(source => source.Name)
            .Select(source => new CrmCatalogItem(source.Id, source.Name, source.Code, source.IsSystem, source.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<CrmLostReason?> GetLostReasonAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.CrmLostReasons.SingleOrDefaultAsync(reason => reason.TenantId == tenantId && reason.Id == id, cancellationToken);

    public Task<bool> LostReasonNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var value = name.Trim();
        return _db.CrmLostReasons.AnyAsync(reason => reason.TenantId == tenantId && reason.Name == value && reason.Id != excludeId, cancellationToken);
    }

    public async Task AddLostReasonAsync(CrmLostReason reason, CancellationToken cancellationToken) =>
        await _db.CrmLostReasons.AddAsync(reason, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CrmCatalogItem>> ListLostReasonsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.CrmLostReasons.AsNoTracking()
            .Where(reason => reason.TenantId == tenantId)
            .OrderBy(reason => reason.Name)
            .Select(reason => new CrmCatalogItem(reason.Id, reason.Name, reason.Code, reason.IsSystem, reason.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<BusinessConfigurationResponse> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var config = await GetConfigAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? TenantBusinessConfig.CreateDefault(Guid.Empty, tenantId);
        return new BusinessConfigurationResponse(
            new MembershipConfigDto(config.AllowOverlappingMemberships, config.AllowEarlyRenewal, config.DefaultStartOnJoinDate, config.GracePeriodDays, config.AutoExpireEnabled),
            new FreezeConfigDto(config.MaxFreezeDays, config.MinFreezeDays, config.MaxFreezeOccurrences, config.FreezeRequiresApproval, config.MaxExtensionDays, config.ExtensionRequiresApproval),
            new AttendanceConfigDto(config.DuplicateCheckInMinutes, config.OpeningTime?.ToString("HH\\:mm"), config.ClosingTime?.ToString("HH\\:mm"), config.CheckInGraceMinutes, config.DefaultCheckInMethod),
            new CrmConfigDto(config.DefaultTrialDays, config.FollowUpReminderHours, config.DefaultFollowUpDays),
            await ListCategoriesAsync(tenantId, false, cancellationToken).ConfigureAwait(false),
            await ListDiscountsAsync(tenantId, null, cancellationToken).ConfigureAwait(false),
            await ListLeadSourcesAsync(tenantId, cancellationToken).ConfigureAwait(false),
            await ListLostReasonsAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }
}
