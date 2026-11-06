using LifeFitAI.Domain.Gym;

namespace LifeFitAI.Application.Gym.Crm;

public sealed record LeadListItem(
    Guid Id,
    string FullName,
    string Mobile,
    LeadSource Source,
    LeadStatus Status,
    string? InterestedPackage,
    string? AssignedTo,
    DateTime? NextFollowUpAt,
    DateTime? TrialAt);

public sealed record LeadDetails(
    Guid Id,
    string FullName,
    string Mobile,
    string? Email,
    LeadSource Source,
    LeadStatus Status,
    Guid? InterestedPlanId,
    string? InterestedPackage,
    Guid? AssignedUserId,
    string? AssignedTo,
    DateTime? NextFollowUpAt,
    string? Notes,
    Guid? ConvertedMemberId,
    DateTime? ConvertedAt,
    LeadLostReason? LostReason,
    DateTime? LostAt,
    DateTime CreatedAt,
    Guid? ConvertedByUserId = null,
    string? ConvertedBy = null);

public sealed record FollowUpItem(Guid Id, Guid LeadId, DateTime ScheduledAt, DateTime? CompletedAt, string? Notes, string? Outcome, string? LeadName);

public sealed record TrialItem(Guid Id, Guid LeadId, DateTime ScheduledAt, TrialStatus Status, string? Notes, string? Outcome);

public sealed record CrmSummary(
    int TotalLeads,
    int NewThisMonth,
    int FollowUpsToday,
    int OverdueFollowUps,
    int TrialsScheduled,
    int TrialsCompleted,
    int Converted,
    int Lost);

public sealed record LeadSearch(
    string? Term,
    LeadStatus? Status,
    LeadSource? Source,
    Guid? AssignedUserId,
    Guid? PlanId,
    DateTime? FollowUpFrom,
    DateTime? FollowUpTo,
    TrialStatus? TrialStatus,
    string? Sort,
    int Page,
    int PageSize);
