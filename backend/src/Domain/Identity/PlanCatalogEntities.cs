using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Identity;

public sealed class PlanDefinition : BaseEntity
{
    private PlanDefinition()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string AppliesTo { get; private set; } = "Any";

    public PlanStatus Status { get; private set; }

    public int SortOrder { get; private set; }

    public int BaseCapacity { get; private set; }

    public static PlanDefinition Create(Guid id, string code, string name, string? description, string appliesTo, int sortOrder, int baseCapacity)
    {
        return new PlanDefinition
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description,
            AppliesTo = appliesTo,
            Status = PlanStatus.Draft,
            SortOrder = sortOrder,
            BaseCapacity = baseCapacity,
        };
    }

    public void Update(string name, string? description, string appliesTo, int sortOrder, int baseCapacity)
    {
        Name = name.Trim();
        Description = description;
        AppliesTo = appliesTo;
        SortOrder = sortOrder;
        BaseCapacity = baseCapacity;
    }

    public bool Activate()
    {
        if (Status == PlanStatus.Retired)
        {
            return false;
        }

        Status = PlanStatus.Active;
        return true;
    }

    public bool Deactivate()
    {
        if (Status != PlanStatus.Active)
        {
            return false;
        }

        Status = PlanStatus.Inactive;
        return true;
    }

    public void Retire() => Status = PlanStatus.Retired;
}

public sealed class FeatureDefinition : BaseEntity
{
    private FeatureDefinition()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Category { get; private set; } = "General";

    public bool IsActive { get; private set; }

    public FeatureScope Scope { get; private set; }

    public bool ConsumesLicense { get; private set; }

    public static FeatureDefinition Create(Guid id, string code, string name, string? description, string category, FeatureScope scope, bool consumesLicense)
    {
        return new FeatureDefinition
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description,
            Category = category,
            IsActive = true,
            Scope = scope,
            ConsumesLicense = consumesLicense,
        };
    }

    public void Update(string name, string? description, string category, bool isActive, FeatureScope scope, bool consumesLicense)
    {
        Name = name.Trim();
        Description = description;
        Category = category;
        IsActive = isActive;
        Scope = scope;
        ConsumesLicense = consumesLicense;
    }
}

public sealed class PlanFeature
{
    private PlanFeature()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public string FeatureCode { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static PlanFeature Create(Guid id, Guid planId, string featureCode, bool enabled, DateTime utcNow) =>
        new()
        {
            Id = id,
            PlanId = planId,
            FeatureCode = featureCode,
            Enabled = enabled,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    public void SetEnabled(bool enabled, DateTime utcNow)
    {
        Enabled = enabled;
        UpdatedAt = utcNow;
    }
}
