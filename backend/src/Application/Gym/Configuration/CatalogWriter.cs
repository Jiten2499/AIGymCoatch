using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym.Configuration;

internal sealed class CatalogWriter
{
    private readonly IOperationGuard _guard;
    private readonly ICurrentActor _actor;
    private readonly IBusinessConfigRepository _configs;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IClientContext _client;
    private readonly IMembershipRepository? _plans;

    public CatalogWriter(IOperationGuard guard, ICurrentActor actor, IBusinessConfigRepository configs, IAuditLogRepository audit, IUnitOfWork unitOfWork, IDateTimeProvider clock, IClientContext client, IMembershipRepository? plans = null)
    {
        _guard = guard;
        _actor = actor;
        _configs = configs;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _client = client;
        _plans = plans;
    }

    public async Task<Result<MemberCategoryItem>> CreateCategory(CreateMemberCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<MemberCategoryItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        if (await _configs.CategoryNameExistsAsync(tenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MemberCategoryItem>(GymErrors.Conflict);
        }

        var category = MemberCategory.Create(Guid.NewGuid(), tenantId, request.Name, request.Description);
        await _configs.AddCategoryAsync(category, cancellationToken).ConfigureAwait(false);
        await Audit(tenantId, "MemberCategoryCreated", category.Id, category.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success(new MemberCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }

    public async Task<Result<MemberCategoryItem>> UpdateCategory(UpdateMemberCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<MemberCategoryItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var category = await _configs.GetCategoryAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure<MemberCategoryItem>(GymErrors.NotFound);
        }

        if (await _configs.CategoryNameExistsAsync(tenantId, request.Name, category.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<MemberCategoryItem>(GymErrors.Conflict);
        }

        category.Update(request.Name, request.Description);
        await Audit(tenantId, "MemberCategoryUpdated", category.Id, category.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success(new MemberCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }

    public async Task<Result<MemberCategoryItem>> ChangeCategoryStatus(ChangeMemberCategoryStatusCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<MemberCategoryItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var category = await _configs.GetCategoryAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure<MemberCategoryItem>(GymErrors.NotFound);
        }

        if (!request.Active && await _configs.CategoryReferencedAsync(tenantId, category.Id, cancellationToken).ConfigureAwait(false))
        {
            category.Deactivate();
        }
        else if (request.Active)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        await Audit(tenantId, "MemberCategoryStatusChanged", category.Id, category.IsActive.ToString(), cancellationToken).ConfigureAwait(false);
        return Result.Success(new MemberCategoryItem(category.Id, category.Name, category.Description, category.IsActive));
    }

    public async Task<Result<DiscountRuleItem>> CreateDiscount(CreateDiscountRuleCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<DiscountRuleItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        if (await ValidateRefs(tenantId, request.AppliesTo, request.PackageId, request.MemberCategoryId, cancellationToken).ConfigureAwait(false) is Error refs)
        {
            return Result.Failure<DiscountRuleItem>(refs);
        }

        if (await _configs.DiscountNameExistsAsync(tenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<DiscountRuleItem>(GymErrors.Conflict);
        }

        var rule = DiscountRule.Create(Guid.NewGuid(), tenantId, request.Name, request.Description, request.DiscountType, request.Value, request.AppliesTo, request.PackageId, request.PackageCategory, request.MemberCategoryId, request.MinimumPackagePrice, request.MinimumDurationDays, request.MaximumDiscountAmount, request.StartDate, request.EndDate);
        await _configs.AddDiscountAsync(rule, cancellationToken).ConfigureAwait(false);
        await Audit(tenantId, "DiscountRuleCreated", rule.Id, rule.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success((await _configs.ListDiscountsAsync(tenantId, null, cancellationToken).ConfigureAwait(false)).Single(item => item.Id == rule.Id));
    }

    public async Task<Result<DiscountRuleItem>> UpdateDiscount(UpdateDiscountRuleCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<DiscountRuleItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var rule = await _configs.GetDiscountAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return Result.Failure<DiscountRuleItem>(GymErrors.NotFound);
        }

        if (await ValidateRefs(tenantId, request.AppliesTo, request.PackageId, request.MemberCategoryId, cancellationToken).ConfigureAwait(false) is Error refs)
        {
            return Result.Failure<DiscountRuleItem>(refs);
        }

        if (await _configs.DiscountNameExistsAsync(tenantId, request.Name, rule.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<DiscountRuleItem>(GymErrors.Conflict);
        }

        rule.Update(request.Name, request.Description, request.DiscountType, request.Value, request.AppliesTo, request.PackageId, request.PackageCategory, request.MemberCategoryId, request.MinimumPackagePrice, request.MinimumDurationDays, request.MaximumDiscountAmount, request.StartDate, request.EndDate);
        await Audit(tenantId, "DiscountRuleUpdated", rule.Id, rule.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success((await _configs.ListDiscountsAsync(tenantId, null, cancellationToken).ConfigureAwait(false)).Single(item => item.Id == rule.Id));
    }

    public async Task<Result<DiscountRuleItem>> ChangeDiscountStatus(ChangeDiscountStatusCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<DiscountRuleItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var rule = await _configs.GetDiscountAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return Result.Failure<DiscountRuleItem>(GymErrors.NotFound);
        }

        if (request.Active)
        {
            rule.Activate();
        }
        else
        {
            rule.Deactivate();
        }

        await Audit(tenantId, "DiscountRuleStatusChanged", rule.Id, rule.IsActive.ToString(), cancellationToken).ConfigureAwait(false);
        return Result.Success((await _configs.ListDiscountsAsync(tenantId, null, cancellationToken).ConfigureAwait(false)).Single(item => item.Id == rule.Id));
    }

    public async Task<Result<CrmCatalogItem>> CreateLeadSource(CreateCrmLeadSourceCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<CrmCatalogItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        if (await _configs.LeadSourceNameExistsAsync(tenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<CrmCatalogItem>(GymErrors.Conflict);
        }

        var source = CrmLeadSource.Create(Guid.NewGuid(), tenantId, request.Name, null, false);
        await _configs.AddLeadSourceAsync(source, cancellationToken).ConfigureAwait(false);
        await Audit(tenantId, "CrmLeadSourceCreated", source.Id, source.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success(new CrmCatalogItem(source.Id, source.Name, source.Code, source.IsSystem, source.IsActive));
    }

    public async Task<Result<CrmCatalogItem>> ChangeLeadSourceStatus(ChangeCrmLeadSourceStatusCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<CrmCatalogItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var source = await _configs.GetLeadSourceAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return Result.Failure<CrmCatalogItem>(GymErrors.NotFound);
        }

        if (request.Active)
        {
            source.Activate();
        }
        else
        {
            source.Deactivate();
        }

        await Audit(tenantId, "CrmLeadSourceStatusChanged", source.Id, source.IsActive.ToString(), cancellationToken).ConfigureAwait(false);
        return Result.Success(new CrmCatalogItem(source.Id, source.Name, source.Code, source.IsSystem, source.IsActive));
    }

    public async Task<Result<CrmCatalogItem>> CreateLostReason(CreateCrmLostReasonCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<CrmCatalogItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        if (await _configs.LostReasonNameExistsAsync(tenantId, request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<CrmCatalogItem>(GymErrors.Conflict);
        }

        var reason = CrmLostReason.Create(Guid.NewGuid(), tenantId, request.Name, null, false);
        await _configs.AddLostReasonAsync(reason, cancellationToken).ConfigureAwait(false);
        await Audit(tenantId, "CrmLostReasonCreated", reason.Id, reason.Name, cancellationToken).ConfigureAwait(false);
        return Result.Success(new CrmCatalogItem(reason.Id, reason.Name, reason.Code, reason.IsSystem, reason.IsActive));
    }

    public async Task<Result<CrmCatalogItem>> ChangeLostReasonStatus(ChangeCrmLostReasonStatusCommand request, CancellationToken cancellationToken)
    {
        if (await Gate(cancellationToken).ConfigureAwait(false) is not Guid tenantId)
        {
            return Result.Failure<CrmCatalogItem>(await Denied(cancellationToken).ConfigureAwait(false));
        }

        var reason = await _configs.GetLostReasonAsync(tenantId, request.Id, cancellationToken).ConfigureAwait(false);
        if (reason is null)
        {
            return Result.Failure<CrmCatalogItem>(GymErrors.NotFound);
        }

        if (request.Active)
        {
            reason.Activate();
        }
        else
        {
            reason.Deactivate();
        }

        await Audit(tenantId, "CrmLostReasonStatusChanged", reason.Id, reason.IsActive.ToString(), cancellationToken).ConfigureAwait(false);
        return Result.Success(new CrmCatalogItem(reason.Id, reason.Name, reason.Code, reason.IsSystem, reason.IsActive));
    }

    private async Task<Guid?> Gate(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ConfigurationManage, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure || _actor.TenantId is not Guid tenantId ? null : tenantId;
    }

    private async Task<Error> Denied(CancellationToken cancellationToken)
    {
        var allowed = await _guard.AllowAsync(PermissionCodes.ConfigurationManage, FeatureCodes.GymConfiguration, cancellationToken).ConfigureAwait(false);
        return allowed.Error;
    }

    private async Task<Error?> ValidateRefs(Guid tenantId, DiscountAppliesTo appliesTo, Guid? packageId, Guid? memberCategoryId, CancellationToken cancellationToken)
    {
        if (appliesTo == DiscountAppliesTo.SpecificPackage)
        {
            if (_plans is null || packageId is not Guid id || await _plans.GetPlanAsync(tenantId, id, cancellationToken).ConfigureAwait(false) is not { IsActive: true })
            {
                return GymErrors.NotFound;
            }
        }

        if (appliesTo == DiscountAppliesTo.MemberCategory)
        {
            if (memberCategoryId is not Guid categoryId || await _configs.GetCategoryAsync(tenantId, categoryId, cancellationToken).ConfigureAwait(false) is not { IsActive: true })
            {
                return GymErrors.NotFound;
            }
        }

        return null;
    }

    private async Task Audit(Guid tenantId, string action, Guid entityId, string detail, CancellationToken cancellationToken)
    {
        await _audit.AddAsync(AuditLog.Create(Guid.NewGuid(), tenantId, _actor.UserId, action, "BusinessConfiguration", entityId, null, detail, _client.IpAddress, _client.UserAgent, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
