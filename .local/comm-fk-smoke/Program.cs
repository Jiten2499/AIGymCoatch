using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Persistence.Interceptors;
using LifeFitAI.Application.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var cs = "Server=(localdb)\\MSSQLLocalDB;Database=LifeFitAI;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
await using (var probe = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cs).Options))
{
    var tenantId = await probe.TrainerProfiles.AsNoTracking().Where(t => t.Status == ProfileStatus.Active).Select(t => t.TenantId).FirstAsync();
    var userId = await probe.TrainerProfiles.AsNoTracking().Where(t => t.TenantId == tenantId && t.Status == ProfileStatus.Active).Select(t => t.UserId).FirstAsync();
    var actorUser = await probe.Users.AsNoTracking().Where(u => u.Id != userId).Select(u => u.Id).FirstAsync();

    var services = new ServiceCollection();
    services.AddSingleton<IDateTimeProvider>(new Clock());
    services.AddSingleton<ICurrentActor>(new Actor(actorUser, tenantId));
    services.AddSingleton<AuditableEntityInterceptor>();
    services.AddDbContext<ApplicationDbContext>((sp, opt) =>
    {
        opt.UseSqlServer(cs);
        opt.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
    });

    await using var sp = services.BuildServiceProvider();
    await using var db = sp.GetRequiredService<ApplicationDbContext>();
    var communicationId = Guid.NewGuid();
    var recipientId = Guid.NewGuid();
    var now = DateTime.UtcNow;
    var communication = Communication.Create(communicationId, tenantId, actorUser, CommunicationType.Announcement, "Smoke", "Body", CommunicationPriority.Normal, CommunicationAudienceType.AllTrainers, null, null, null, null, null);
    communication.Publish(now);
    db.Communications.Add(communication);
    db.CommunicationRecipients.Add(CommunicationRecipient.Create(recipientId, tenantId, communicationId, userId, CommunicationRecipientKind.Trainer, null, now));
    try
    {
        await db.SaveChangesAsync();
        var ok = await db.Communications.AnyAsync(c => c.Id == communicationId) && await db.CommunicationRecipients.AnyAsync(r => r.Id == recipientId);
        Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_MISSING");
    }
    catch (Exception ex)
    {
        Console.WriteLine("SMOKE_FAIL " + ex.GetBaseException().Message);
    }
    finally
    {
        var recipient = await db.CommunicationRecipients.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == recipientId);
        var row = await db.Communications.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == communicationId);
        if (recipient is not null) db.CommunicationRecipients.Remove(recipient);
        if (row is not null) db.Communications.Remove(row);
        await db.SaveChangesAsync();
    }
}

sealed class Clock : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }
sealed class Actor(Guid userId, Guid tenantId) : ICurrentActor
{
    public Guid? UserId { get; } = userId;
    public Guid? TenantId { get; } = tenantId;
}
