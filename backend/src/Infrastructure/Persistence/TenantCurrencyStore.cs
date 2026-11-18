using LifeFitAI.Application.Gym.Configuration;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class TenantCurrencyStore : ITenantCurrencyStore
{
    private readonly ApplicationDbContext _db;

    public TenantCurrencyStore(ApplicationDbContext db) => _db = db;

    public async Task ApplyAsync(Guid tenantId, string currency, CancellationToken cancellationToken)
    {
        var profile = await _db.BusinessProfiles.SingleOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            await _db.BusinessProfiles.AddAsync(BusinessProfile.Create(Guid.NewGuid(), tenantId, "UTC", currency), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            profile.SetCurrency(currency);
        }

        await _db.MembershipPlans.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.GymMemberships.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.MembershipPayments.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.PtPackages.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.MemberPtAssignments.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.MemberPtPayments.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.Expenses.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.FinancialAccounts.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.JournalEntries.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.PayrollRuns.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.StaffCompensation.IgnoreQueryFilters().Where(item => item.TenantId == tenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
        await _db.ApprovalRequests.IgnoreQueryFilters().Where(item => item.TenantId == tenantId && item.Currency != null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Currency, currency), cancellationToken).ConfigureAwait(false);
    }
}
