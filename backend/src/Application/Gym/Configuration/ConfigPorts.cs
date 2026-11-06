using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Configuration;

public interface IBusinessConfigRepository
{
    Task<TenantBusinessConfig?> GetConfigAsync(Guid tenantId, CancellationToken cancellationToken);

    Task AddConfigAsync(TenantBusinessConfig config, CancellationToken cancellationToken);

    Task<MemberCategory?> GetCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> CategoryNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddCategoryAsync(MemberCategory category, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberCategoryItem>> ListCategoriesAsync(Guid tenantId, bool activeOnly, CancellationToken cancellationToken);

    Task<bool> CategoryReferencedAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken);

    Task<DiscountRule?> GetDiscountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> DiscountNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddDiscountAsync(DiscountRule rule, CancellationToken cancellationToken);

    Task<IReadOnlyList<DiscountRuleItem>> ListDiscountsAsync(Guid tenantId, bool? activeOnly, CancellationToken cancellationToken);

    Task<CrmLeadSource?> GetLeadSourceAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> LeadSourceNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddLeadSourceAsync(CrmLeadSource source, CancellationToken cancellationToken);

    Task<IReadOnlyList<CrmCatalogItem>> ListLeadSourcesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<CrmLostReason?> GetLostReasonAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> LostReasonNameExistsAsync(Guid tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddLostReasonAsync(CrmLostReason reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<CrmCatalogItem>> ListLostReasonsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<BusinessConfigurationResponse> ReadAsync(Guid tenantId, CancellationToken cancellationToken);
}
