using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

internal static class SeatAssignmentLock
{
    public static async Task AcquireAsync(ApplicationDbContext db, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null)
        {
            return;
        }

        var resource = $"lifefit-seat:{tenantId:N}:{userId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
IF @result < 0 THROW 50001, N'Could not lock the business seat for this person.', 1;",
            cancellationToken).ConfigureAwait(false);
    }
}
