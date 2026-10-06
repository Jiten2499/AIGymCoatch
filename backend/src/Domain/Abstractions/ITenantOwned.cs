namespace LifeFitAI.Domain.Abstractions;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
