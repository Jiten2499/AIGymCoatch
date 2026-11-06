using System.Text.Json;
using LifeFitAI.Application.Platform.SystemJobs;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Platform;

public sealed class MembershipExpiryNotificationJob
{
    private readonly ApplicationDbContext _db;
    private readonly IMembershipExpiryProcessor _processor;

    public MembershipExpiryNotificationJob(ApplicationDbContext db, IMembershipExpiryProcessor processor)
    {
        _db = db;
        _processor = processor;
    }

    public async Task<string> RunAsync(bool manual)
    {
        var definition = await _db.SystemJobDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(job => job.JobKey == SystemJobCatalog.MembershipExpiryKey)
            .ConfigureAwait(false);
        if (definition is null || !definition.IsEnabled)
        {
            return JsonSerializer.Serialize(new MembershipExpiryRun(0, 0, 0, 0, 0, 0, true));
        }

        _ = manual;
        return JsonSerializer.Serialize(await _processor.ExecuteAsync(CancellationToken.None).ConfigureAwait(false));
    }
}
