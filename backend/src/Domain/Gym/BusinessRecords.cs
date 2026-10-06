using LifeFitAI.SharedKernel.Primitives;

namespace LifeFitAI.Domain.Gym;

public sealed class BusinessProfile : BaseEntity
{
    private BusinessProfile()
    {
    }

    public Guid TenantId { get; private set; }

    public string? LegalName { get; private set; }

    public string? Description { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Website { get; private set; }

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? Country { get; private set; }

    public string? PostalCode { get; private set; }

    public string? LogoUrl { get; private set; }

    public string TimeZone { get; private set; } = "UTC";

    public string Currency { get; private set; } = "USD";

    public static BusinessProfile Create(Guid id, Guid tenantId, string timeZone, string currency)
    {
        return new BusinessProfile
        {
            Id = id,
            TenantId = tenantId,
            TimeZone = timeZone,
            Currency = currency,
        };
    }

    public void Update(
        string? legalName,
        string? description,
        string? email,
        string? phone,
        string? website,
        string? address,
        string? city,
        string? state,
        string? country,
        string? postalCode,
        string? logoUrl,
        string timeZone,
        string currency)
    {
        LegalName = legalName;
        Description = description;
        Email = email;
        Phone = phone;
        Website = website;
        Address = address;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
        LogoUrl = logoUrl;
        TimeZone = timeZone;
        Currency = currency;
    }
}

public sealed class Branch : BaseEntity
{
    private Branch()
    {
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? Country { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string TimeZone { get; private set; } = "UTC";

    public BranchStatus Status { get; private set; }

    public static Branch Create(Guid id, Guid tenantId, string name, string code, string timeZone)
    {
        return new Branch
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Code = code,
            TimeZone = timeZone,
            Status = BranchStatus.Active,
        };
    }

    public void Update(
        string name,
        string? address,
        string? city,
        string? state,
        string? country,
        string? postalCode,
        string? phone,
        string? email,
        string timeZone)
    {
        Name = name;
        Address = address;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
        Phone = phone;
        Email = email;
        TimeZone = timeZone;
    }

    public void Deactivate() => Status = BranchStatus.Inactive;
}
