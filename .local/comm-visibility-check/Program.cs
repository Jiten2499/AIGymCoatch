using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var cs = "Server=(localdb)\\MSSQLLocalDB;Database=LifeFitAI;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cs).Options);

var latest = await db.Communications.AsNoTracking().OrderByDescending(c => c.CreatedAt).FirstAsync();
var memberUserId = await db.CommunicationRecipients.AsNoTracking()
    .Where(r => r.CommunicationId == latest.Id)
    .Select(r => r.UserId)
    .FirstAsync();
var repo = new CommunicationRepository(db);
var page = await repo.ListInboxAsync(latest.TenantId, memberUserId, null, false, 1, 20, DateTime.UtcNow, CancellationToken.None);
var ownerPage = await repo.ListInboxAsync(latest.TenantId, Guid.Parse("c81b9921-f514-4fea-a173-8c61207f3a0e"), null, false, 1, 20, DateTime.UtcNow, CancellationToken.None);
var trainerUserId = await db.TrainerProfiles.AsNoTracking()
    .Where(t => t.TenantId == latest.TenantId && t.Status == ProfileStatus.Active)
    .Select(t => t.UserId)
    .FirstAsync();
var trainerPage = await repo.ListInboxAsync(latest.TenantId, trainerUserId, null, false, 1, 20, DateTime.UtcNow, CancellationToken.None);

Console.WriteLine($"memberInbox={page.TotalCount}; titles={string.Join('|', page.Items.Select(i => i.Title))}");
Console.WriteLine($"ownerInbox={ownerPage.TotalCount}");
Console.WriteLine($"trainerInbox={trainerPage.TotalCount}");
