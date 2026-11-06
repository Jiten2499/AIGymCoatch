namespace LifeFitAI.Application.Authorization;

public sealed record MemberAccessLimit(string Scope, Guid? OnlyUserId, Guid? AssignedTrainerId, bool Blocked)
{
    public static MemberAccessLimit AllowAll() => new(Domain.Identity.AccessScopes.All, null, null, false);

    public static MemberAccessLimit Block() => new(Domain.Identity.AccessScopes.Assigned, null, null, true);
}

public interface IMemberAccessScope
{
    Task<MemberAccessLimit> LimitAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
