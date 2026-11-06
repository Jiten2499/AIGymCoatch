using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Crm;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class LeadRepository : ILeadRepository
{
    private readonly ApplicationDbContext _db;

    public LeadRepository(ApplicationDbContext db) => _db = db;

    public Task<Lead?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Leads.SingleOrDefaultAsync(lead => lead.TenantId == tenantId && lead.Id == id, cancellationToken);

    public Task<bool> HasOpenMobileAsync(Guid tenantId, string mobile, Guid? excludeLeadId, CancellationToken cancellationToken) =>
        _db.Leads.AnyAsync(lead => lead.TenantId == tenantId && lead.Mobile == mobile && lead.Id != excludeLeadId && lead.Status != LeadStatus.Lost && lead.Status != LeadStatus.Converted, cancellationToken);

    public async Task AddAsync(Lead lead, CancellationToken cancellationToken) =>
        await _db.Leads.AddAsync(lead, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<LeadListItem>> SearchAsync(Guid tenantId, LeadSearch search, CancellationToken cancellationToken)
    {
        var query = _db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenantId);
        if (search.Status is not null)
        {
            query = query.Where(lead => lead.Status == search.Status);
        }

        if (search.Source is not null)
        {
            query = query.Where(lead => lead.Source == search.Source);
        }

        if (search.AssignedUserId is not null)
        {
            query = query.Where(lead => lead.AssignedUserId == search.AssignedUserId);
        }

        if (search.PlanId is not null)
        {
            query = query.Where(lead => lead.InterestedPlanId == search.PlanId);
        }

        if (search.FollowUpFrom is not null)
        {
            query = query.Where(lead => lead.NextFollowUpAt >= search.FollowUpFrom);
        }

        if (search.FollowUpTo is not null)
        {
            query = query.Where(lead => lead.NextFollowUpAt < search.FollowUpTo);
        }

        if (!string.IsNullOrWhiteSpace(search.Term))
        {
            var value = search.Term.Trim();
            query = query.Where(lead => lead.FullName.Contains(value) || lead.Mobile.Contains(value) || (lead.Email != null && lead.Email.Contains(value)));
        }

        if (search.TrialStatus is not null)
        {
            var trialStatus = search.TrialStatus.Value;
            query = query.Where(lead => _db.LeadTrials.Any(trial => trial.TenantId == tenantId && trial.LeadId == lead.Id && trial.Status == trialStatus));
        }

        query = search.Sort switch
        {
            "name" => query.OrderBy(lead => lead.FullName),
            "followUp" => query.OrderBy(lead => lead.NextFollowUpAt),
            _ => query.OrderByDescending(lead => lead.CreatedAt),
        };
        var page = new PageQuery(search.Page, search.PageSize);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var leads = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await NamesAsync(leads.Select(lead => lead.InterestedPlanId).ToArray(), leads.Select(lead => lead.AssignedUserId).ToArray(), cancellationToken).ConfigureAwait(false);
        var ids = leads.Select(lead => lead.Id).ToArray();
        var trials = ids.Length == 0
            ? []
            : await _db.LeadTrials.AsNoTracking()
                .Where(trial => ids.Contains(trial.LeadId) && trial.Status == TrialStatus.Scheduled)
                .GroupBy(trial => trial.LeadId)
                .Select(group => new { group.Key, At = group.Min(trial => trial.ScheduledAt) })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        var trialAt = trials.ToDictionary(item => item.Key, item => (DateTime?)item.At);
        var items = leads.Select(lead => new LeadListItem(lead.Id, lead.FullName, lead.Mobile, lead.Source, lead.Status, Name(names.Plans, lead.InterestedPlanId), Person(names.People, lead.AssignedUserId), lead.NextFollowUpAt, trialAt.GetValueOrDefault(lead.Id))).ToArray();
        return new PageResult<LeadListItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<LeadDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken).ConfigureAwait(false);
        if (lead is null)
        {
            return null;
        }

        var names = await NamesAsync([lead.InterestedPlanId], [lead.AssignedUserId, lead.ConvertedByUserId], cancellationToken).ConfigureAwait(false);
        return new LeadDetails(lead.Id, lead.FullName, lead.Mobile, lead.Email, lead.Source, lead.Status, lead.InterestedPlanId, Name(names.Plans, lead.InterestedPlanId), lead.AssignedUserId, Person(names.People, lead.AssignedUserId), lead.NextFollowUpAt, lead.Notes, lead.ConvertedMemberId, lead.ConvertedAt, lead.LostReason, lead.LostAt, lead.CreatedAt, lead.ConvertedByUserId, Person(names.People, lead.ConvertedByUserId));
    }

    public async Task AddFollowUpAsync(LeadFollowUp followUp, CancellationToken cancellationToken) =>
        await _db.LeadFollowUps.AddAsync(followUp, cancellationToken).ConfigureAwait(false);

    public Task<LeadFollowUp?> GetFollowUpAsync(Guid tenantId, Guid leadId, Guid id, CancellationToken cancellationToken) =>
        _db.LeadFollowUps.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.LeadId == leadId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FollowUpItem>> ListFollowUpsAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken)
    {
        return await _db.LeadFollowUps.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LeadId == leadId)
            .OrderByDescending(item => item.ScheduledAt)
            .Select(item => new FollowUpItem(item.Id, item.LeadId, item.ScheduledAt, item.CompletedAt, item.Notes, item.Outcome, null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PageResult<FollowUpItem>> QueueAsync(Guid tenantId, string bucket, DateTime todayStartUtc, DateTime tomorrowStartUtc, PageQuery page, CancellationToken cancellationToken)
    {
        var query = from followUp in _db.LeadFollowUps.AsNoTracking()
                    join lead in _db.Leads.AsNoTracking() on followUp.LeadId equals lead.Id
                    where followUp.TenantId == tenantId && followUp.CompletedAt == null
                        && lead.Status != LeadStatus.Converted && lead.Status != LeadStatus.Lost
                    select new { followUp, lead };
        query = bucket switch
        {
            "overdue" => query.Where(row => row.followUp.ScheduledAt < todayStartUtc),
            "upcoming" => query.Where(row => row.followUp.ScheduledAt >= tomorrowStartUtc),
            _ => query.Where(row => row.followUp.ScheduledAt >= todayStartUtc && row.followUp.ScheduledAt < tomorrowStartUtc),
        };
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(row => row.followUp.ScheduledAt).Skip(page.Skip).Take(page.NormalizedSize)
            .Select(row => new FollowUpItem(row.followUp.Id, row.followUp.LeadId, row.followUp.ScheduledAt, row.followUp.CompletedAt, row.followUp.Notes, row.followUp.Outcome, row.lead.FullName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<FollowUpItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<DateTime?> NextOpenFollowUpAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken) =>
        _db.LeadFollowUps.Where(item => item.TenantId == tenantId && item.LeadId == leadId && item.CompletedAt == null)
            .OrderBy(item => item.ScheduledAt)
            .Select(item => (DateTime?)item.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddTrialAsync(LeadTrial trial, CancellationToken cancellationToken) =>
        await _db.LeadTrials.AddAsync(trial, cancellationToken).ConfigureAwait(false);

    public Task<LeadTrial?> GetTrialAsync(Guid tenantId, Guid leadId, Guid id, CancellationToken cancellationToken) =>
        _db.LeadTrials.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.LeadId == leadId && item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TrialItem>> ListTrialsAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken) =>
        await _db.LeadTrials.AsNoTracking().Where(item => item.TenantId == tenantId && item.LeadId == leadId)
            .OrderByDescending(item => item.ScheduledAt)
            .Select(item => new TrialItem(item.Id, item.LeadId, item.ScheduledAt, item.Status, item.Notes, item.Outcome))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<CrmSummary> SummarizeAsync(Guid tenantId, DateTime todayStartUtc, DateTime tomorrowStartUtc, DateTime monthStartUtc, CancellationToken cancellationToken)
    {
        var leads = _db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenantId);
        var openFollowUps = _db.LeadFollowUps.AsNoTracking().Where(item => item.TenantId == tenantId && item.CompletedAt == null);
        var trials = _db.LeadTrials.AsNoTracking().Where(item => item.TenantId == tenantId);
        return new CrmSummary(
            await leads.CountAsync(cancellationToken).ConfigureAwait(false),
            await leads.CountAsync(lead => lead.CreatedAt >= monthStartUtc, cancellationToken).ConfigureAwait(false),
            await openFollowUps.CountAsync(item => item.ScheduledAt >= todayStartUtc && item.ScheduledAt < tomorrowStartUtc, cancellationToken).ConfigureAwait(false),
            await openFollowUps.CountAsync(item => item.ScheduledAt < todayStartUtc, cancellationToken).ConfigureAwait(false),
            await trials.CountAsync(item => item.Status == TrialStatus.Scheduled, cancellationToken).ConfigureAwait(false),
            await trials.CountAsync(item => item.Status == TrialStatus.Completed, cancellationToken).ConfigureAwait(false),
            await leads.CountAsync(lead => lead.Status == LeadStatus.Converted, cancellationToken).ConfigureAwait(false),
            await leads.CountAsync(lead => lead.Status == LeadStatus.Lost, cancellationToken).ConfigureAwait(false));
    }

    private async Task<(Dictionary<Guid, string> Plans, Dictionary<Guid, string> People)> NamesAsync(Guid?[] planIds, Guid?[] userIds, CancellationToken cancellationToken)
    {
        var plans = planIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var people = userIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var planNames = plans.Length == 0
            ? []
            : await _db.MembershipPlans.AsNoTracking().Where(plan => plans.Contains(plan.Id)).ToDictionaryAsync(plan => plan.Id, plan => plan.Name, cancellationToken).ConfigureAwait(false);
        var personNames = people.Length == 0
            ? []
            : await _db.Users.AsNoTracking().Where(user => people.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FirstName + " " + user.LastName, cancellationToken).ConfigureAwait(false);
        return (planNames, personNames);
    }

    private static string? Name(Dictionary<Guid, string> names, Guid? id) => id is Guid key && names.TryGetValue(key, out var name) ? name : null;

    private static string? Person(Dictionary<Guid, string> names, Guid? id) => Name(names, id);
}
