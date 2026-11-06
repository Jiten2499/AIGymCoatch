using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Gym;

public interface IBusinessProfileSoftDelete
{
    Task<Result> DeleteMemberAsync(Guid tenantId, Guid actorId, Guid memberProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task<Result> DeleteTrainerAsync(Guid tenantId, Guid actorId, Guid trainerProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task<Result> DeleteStaffAsync(Guid tenantId, Guid actorId, Guid staffProfileId, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task<Result> DeleteStaffUsersAsync(Guid tenantId, Guid actorId, IReadOnlyList<Guid> userIds, DateTime utcNow, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
}
