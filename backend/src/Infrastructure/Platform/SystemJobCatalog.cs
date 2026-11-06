using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class EfSystemJobCatalog : ISystemJobCatalog
{
    private readonly ApplicationDbContext _db;

    public EfSystemJobCatalog(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SystemJobDefinition>> ListAsync(CancellationToken cancellationToken) =>
        await _db.SystemJobDefinitions.OrderBy(job => job.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<SystemJobDefinition?> FindAsync(string jobKey, CancellationToken cancellationToken) =>
        _db.SystemJobDefinitions.FirstOrDefaultAsync(job => job.JobKey == jobKey, cancellationToken);
}
