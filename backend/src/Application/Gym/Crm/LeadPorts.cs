using LifeFitAI.Application.Gym;
using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Crm;

public interface ILeadRepository
{
    Task<Lead?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> HasOpenMobileAsync(Guid tenantId, string mobile, Guid? excludeLeadId, CancellationToken cancellationToken);

    Task AddAsync(Lead lead, CancellationToken cancellationToken);

    Task<PageResult<LeadListItem>> SearchAsync(Guid tenantId, LeadSearch search, CancellationToken cancellationToken);

    Task<LeadDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task AddFollowUpAsync(LeadFollowUp followUp, CancellationToken cancellationToken);

    Task<LeadFollowUp?> GetFollowUpAsync(Guid tenantId, Guid leadId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FollowUpItem>> ListFollowUpsAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken);

    Task<PageResult<FollowUpItem>> QueueAsync(Guid tenantId, string bucket, DateTime todayStartUtc, DateTime tomorrowStartUtc, PageQuery page, CancellationToken cancellationToken);

    Task<DateTime?> NextOpenFollowUpAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken);

    Task AddTrialAsync(LeadTrial trial, CancellationToken cancellationToken);

    Task<LeadTrial?> GetTrialAsync(Guid tenantId, Guid leadId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrialItem>> ListTrialsAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken);

    Task<CrmSummary> SummarizeAsync(Guid tenantId, DateTime todayStartUtc, DateTime tomorrowStartUtc, DateTime monthStartUtc, CancellationToken cancellationToken);
}
