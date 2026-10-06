namespace LifeFitAI.Domain.Identity;

/// <summary>
/// Well-known identifiers for the single platform tenant that owns the super admin
/// and hosts every self-registered normal user. Business tenants are created at runtime.
/// </summary>
public static class PlatformConstants
{
    /// <summary>The one and only platform tenant ("LifeFit AI").</summary>
    public static readonly Guid PlatformTenantId = Guid.Parse("33333333-3333-4333-8333-333333333001");

    public const string PlatformTenantName = "LifeFit AI";
}
