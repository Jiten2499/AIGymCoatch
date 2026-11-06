using LifeFitAI.Application.Gym.Payroll;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class PayrollStore : IPayrollStore
{
    private readonly ApplicationDbContext _db;

    public PayrollStore(ApplicationDbContext db) => _db = db;

    public Task<PayrollRun?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken) =>
        _db.PayrollRuns.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == runId, cancellationToken);

    public Task<PayrollRun?> FindRunAsync(Guid tenantId, int year, int month, CancellationToken cancellationToken) =>
        _db.PayrollRuns.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.PeriodYear == year && item.PeriodMonth == month, cancellationToken);

    public async Task<IReadOnlyList<PayrollRunSummary>> ListRunsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var runs = await _db.PayrollRuns.AsNoTracking().Where(item => item.TenantId == tenantId).OrderByDescending(item => item.PeriodYear).ThenByDescending(item => item.PeriodMonth).ToListAsync(cancellationToken).ConfigureAwait(false);
        var lines = await _db.PayrollLines.AsNoTracking().Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return runs.Select(run =>
        {
            var own = lines.Where(line => line.PayrollRunId == run.Id).ToArray();
            return new PayrollRunSummary(
                run.Id, run.PeriodYear, run.PeriodMonth, run.PeriodStart, run.PeriodEnd, run.Currency, run.Status.ToString(),
                own.Length, own.Sum(line => line.GrossPay), own.Sum(line => line.Deductions), own.Sum(line => line.NetPay), own.Sum(line => line.AmountPaid),
                own.Count(line => line.MissingSalary || line.UnapprovedCommission || line.UnconfirmedAttendance));
        }).ToArray();
    }

    public Task AddRunAsync(PayrollRun run, CancellationToken cancellationToken)
    {
        _db.PayrollRuns.Add(run);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PayrollLine>> ListLinesAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken) =>
        await _db.PayrollLines.Where(item => item.TenantId == tenantId && item.PayrollRunId == runId).OrderBy(item => item.EmployeeName).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<PayrollLine?> GetLineAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken) =>
        _db.PayrollLines.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == lineId, cancellationToken);

    public Task AddLineAsync(PayrollLine line, CancellationToken cancellationToken)
    {
        _db.PayrollLines.Add(line);
        return Task.CompletedTask;
    }

    public async Task RemoveLinesAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken)
    {
        var lines = await _db.PayrollLines.Where(item => item.TenantId == tenantId && item.PayrollRunId == runId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = lines.Select(line => line.Id).ToArray();
        var adjustments = await _db.PayrollAdjustments.Where(item => item.TenantId == tenantId && ids.Contains(item.PayrollLineId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        _db.PayrollAdjustments.RemoveRange(adjustments);
        _db.PayrollLines.RemoveRange(lines);
    }

    public async Task RemoveLineAsync(PayrollLine line, CancellationToken cancellationToken)
    {
        var adjustments = await _db.PayrollAdjustments.Where(item => item.TenantId == line.TenantId && item.PayrollLineId == line.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        _db.PayrollAdjustments.RemoveRange(adjustments);
        _db.PayrollLines.Remove(line);
    }

    public async Task<IReadOnlyList<PayrollAdjustment>> ListAdjustmentsAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken) =>
        await _db.PayrollAdjustments.Where(item => item.TenantId == tenantId && item.PayrollLineId == lineId).OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken)
    {
        _db.PayrollAdjustments.Add(adjustment);
        return Task.CompletedTask;
    }

    public Task<PayrollAdjustment?> GetAdjustmentAsync(Guid tenantId, Guid adjustmentId, CancellationToken cancellationToken) =>
        _db.PayrollAdjustments.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == adjustmentId, cancellationToken);

    public Task RemoveAdjustmentAsync(PayrollAdjustment adjustment, DateTime utcNow, CancellationToken cancellationToken)
    {
        adjustment.SoftDelete(utcNow);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PayrollPayment>> ListPaymentsAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken) =>
        await _db.PayrollPayments.AsNoTracking().Where(item => item.TenantId == tenantId && item.PayrollLineId == lineId).OrderBy(item => item.PaymentDate).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> PaymentReferenceExistsAsync(Guid tenantId, Guid lineId, string reference, CancellationToken cancellationToken) =>
        _db.PayrollPayments.AsNoTracking().AnyAsync(item => item.TenantId == tenantId && item.PayrollLineId == lineId && item.Reference == reference, cancellationToken);

    public Task AddPaymentAsync(PayrollPayment payment, CancellationToken cancellationToken)
    {
        _db.PayrollPayments.Add(payment);
        return Task.CompletedTask;
    }

    public async Task<StaffCompensation?> CurrentCompensationAsync(Guid tenantId, Guid staffProfileId, DateOnly onDay, CancellationToken cancellationToken)
    {
        var rows = await _db.StaffCompensation.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.StaffProfileId == staffProfileId && item.EffectiveFrom <= onDay)
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.FirstOrDefault(item => item.Covers(onDay));
    }

    public async Task<StaffCompensation?> TrackedCompensationAsync(Guid tenantId, Guid staffProfileId, DateOnly onDay, CancellationToken cancellationToken)
    {
        var rows = await _db.StaffCompensation
            .Where(item => item.TenantId == tenantId && item.StaffProfileId == staffProfileId && item.EffectiveFrom <= onDay)
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.FirstOrDefault(item => item.Covers(onDay));
    }

    public async Task<IReadOnlyList<StaffCompensation>> ListCompensationAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.StaffCompensation.AsNoTracking().Where(item => item.TenantId == tenantId).OrderByDescending(item => item.EffectiveFrom).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddCompensationAsync(StaffCompensation compensation, CancellationToken cancellationToken)
    {
        _db.StaffCompensation.Add(compensation);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<StaffProfile>> ActiveStaffAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.StaffProfiles.Where(item => item.TenantId == tenantId && item.Status == ProfileStatus.Active).OrderBy(item => item.EmployeeCode).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Dictionary<Guid, string>> UserNamesAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).Select(user => new { user.Id, user.FirstName, user.LastName }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return users.ToDictionary(user => user.Id, user => $"{user.FirstName} {user.LastName}".Trim());
    }

    public async Task<Dictionary<Guid, Guid>> TrainerIdsByUserAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var trainers = await _db.TrainerProfiles.AsNoTracking().Where(item => item.TenantId == tenantId && userIds.Contains(item.UserId) && item.Status == ProfileStatus.Active).Select(item => new { item.UserId, item.Id }).ToListAsync(cancellationToken).ConfigureAwait(false);
        return trainers.GroupBy(item => item.UserId).ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<decimal> ApprovedCommissionsAsync(Guid tenantId, Guid trainerProfileId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var total = await _db.TrainerCompensationCalculations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.TrainerProfileId == trainerProfileId && item.Status == CompensationStatus.Approved && item.PeriodStart <= to && item.PeriodEnd >= from)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken)
            .ConfigureAwait(false);
        return total ?? 0m;
    }

    public Task<bool> HasUnapprovedCommissionAsync(Guid tenantId, Guid trainerProfileId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        _db.TrainerCompensationCalculations.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId && item.TrainerProfileId == trainerProfileId && item.PeriodStart <= to && item.PeriodEnd >= from
                && (item.Status == CompensationStatus.Calculated || item.Status == CompensationStatus.PendingApproval),
            cancellationToken);

    public async Task<string> WorkspaceCurrencyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currency = await _db.BusinessProfiles.AsNoTracking().Where(item => item.TenantId == tenantId).Select(item => item.Currency).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(currency) ? "USD" : currency;
    }
}
