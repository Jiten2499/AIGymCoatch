using LifeFitAI.Domain.Gym;
using LifeFitAI.Infrastructure.Persistence;
using LifeFitAI.Infrastructure.Persistence.Interceptors;
using LifeFitAI.Application.Identity;
using LifeFitAI.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var tenantId = Guid.Parse("2d7cb2f8-169d-4e0f-bfc1-47dad5d3114d");
var userId = Guid.Parse("c81b9921-f514-4fea-a173-8c61207f3a0e");
var cs = "Server=(localdb)\\MSSQLLocalDB;Database=LifeFitAI;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

var services = new ServiceCollection();
services.AddSingleton<IDateTimeProvider>(new Clock());
services.AddSingleton<ICurrentActor>(new Actor(userId, tenantId));
services.AddSingleton<AuditableEntityInterceptor>();
services.AddDbContext<ApplicationDbContext>((sp, opt) =>
{
    opt.UseSqlServer(cs);
    opt.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
});

await using var sp = services.BuildServiceProvider();
await using var db = sp.GetRequiredService<ApplicationDbContext>();

var staff = await db.StaffProfiles.AsNoTracking().FirstAsync(s => s.TenantId == tenantId && s.Status == ProfileStatus.Active);
var runId = Guid.NewGuid();
var lineId = Guid.NewGuid();
var year = 2099;
var month = 12;

db.PayrollRuns.Add(PayrollRun.Create(runId, tenantId, year, month, "USD", userId, DateTime.UtcNow));
db.PayrollLines.Add(PayrollLine.Create(lineId, tenantId, runId, staff.Id, staff.UserId, "Smoke", staff.EmployeeCode, staff.StaffType.ToString(), 1000m, 0m, 0m, false, false, false));

try
{
    await db.SaveChangesAsync();
    var ok = await db.PayrollRuns.AnyAsync(r => r.Id == runId) && await db.PayrollLines.AnyAsync(l => l.Id == lineId);
    Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_MISSING");
}
catch (Exception ex)
{
    Console.WriteLine("SMOKE_FAIL " + ex.GetBaseException().Message);
}
finally
{
    var line = await db.PayrollLines.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == lineId);
    var run = await db.PayrollRuns.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == runId);
    if (line is not null) db.PayrollLines.Remove(line);
    if (run is not null) db.PayrollRuns.Remove(run);
    await db.SaveChangesAsync();
}

sealed class Clock : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }
sealed class Actor(Guid userId, Guid tenantId) : ICurrentActor
{
    public Guid? UserId { get; } = userId;
    public Guid? TenantId { get; } = tenantId;
}
