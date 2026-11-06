using LifeFitAI.Application.Abstractions.Persistence;
using LifeFitAI.Domain.Email;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Platform;
using LifeFitAI.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserType> UserTypes => Set<UserType>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RoleResourceScope> RoleResourceScopes => Set<RoleResourceScope>();

    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    public DbSet<UserResourceScope> UserResourceScopes => Set<UserResourceScope>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<SubscriptionSeat> SubscriptionSeats => Set<SubscriptionSeat>();

    public DbSet<PlanDefinition> Plans => Set<PlanDefinition>();

    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();

    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();

    public DbSet<StaffBranchAccess> StaffBranchAccess => Set<StaffBranchAccess>();

    public DbSet<StaffCompensation> StaffCompensation => Set<StaffCompensation>();

    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();

    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();

    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();

    public DbSet<PayrollPayment> PayrollPayments => Set<PayrollPayment>();

    public DbSet<StaffLeaveRequest> StaffLeaveRequests => Set<StaffLeaveRequest>();

    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

    public DbSet<TrainerMemberAssignment> TrainerMemberAssignments => Set<TrainerMemberAssignment>();

    public DbSet<TrainerTarget> TrainerTargets => Set<TrainerTarget>();

    public DbSet<TrainerIncentiveRule> TrainerIncentiveRules => Set<TrainerIncentiveRule>();

    public DbSet<TrainerCommissionRule> TrainerCommissionRules => Set<TrainerCommissionRule>();

    public DbSet<TrainerPerformanceIncentive> TrainerPerformanceIncentives => Set<TrainerPerformanceIncentive>();

    public DbSet<TrainerCompensationCalculation> TrainerCompensationCalculations => Set<TrainerCompensationCalculation>();

    public DbSet<TrainerRewardSettings> TrainerRewardSettings => Set<TrainerRewardSettings>();

    public DbSet<TrainerTargetDefinition> TrainerTargetDefinitions => Set<TrainerTargetDefinition>();

    public DbSet<TrainerTargetAssignment> TrainerTargetAssignments => Set<TrainerTargetAssignment>();

    public DbSet<TrainerRewardTransaction> TrainerRewardTransactions => Set<TrainerRewardTransaction>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    public DbSet<PtPackage> PtPackages => Set<PtPackage>();

    public DbSet<MemberPtAssignment> MemberPtAssignments => Set<MemberPtAssignment>();

    public DbSet<PtSession> PtSessions => Set<PtSession>();

    public DbSet<PtSessionExercise> PtSessionExercises => Set<PtSessionExercise>();

    public DbSet<PtSessionConsumption> PtSessionConsumptions => Set<PtSessionConsumption>();

    public DbSet<GymMembership> GymMemberships => Set<GymMembership>();

    public DbSet<MembershipHistoryEntry> MembershipHistory => Set<MembershipHistoryEntry>();

    public DbSet<MembershipFreeze> MembershipFreezes => Set<MembershipFreeze>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<LeadFollowUp> LeadFollowUps => Set<LeadFollowUp>();

    public DbSet<LeadTrial> LeadTrials => Set<LeadTrial>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<TenantBusinessConfig> TenantBusinessConfigs => Set<TenantBusinessConfig>();

    public DbSet<MemberCategory> MemberCategories => Set<MemberCategory>();

    public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();

    public DbSet<CrmLeadSource> CrmLeadSources => Set<CrmLeadSource>();

    public DbSet<CrmLostReason> CrmLostReasons => Set<CrmLostReason>();

    public DbSet<Communication> Communications => Set<Communication>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<SystemJobDefinition> SystemJobDefinitions => Set<SystemJobDefinition>();

    public DbSet<MembershipExpiryReminder> MembershipExpiryReminders => Set<MembershipExpiryReminder>();

    public DbSet<CommunicationAudienceMember> CommunicationAudienceMembers => Set<CommunicationAudienceMember>();

    public DbSet<CommunicationRead> CommunicationReads => Set<CommunicationRead>();

    public DbSet<CommunicationAudienceTarget> CommunicationAudienceTargets => Set<CommunicationAudienceTarget>();

    public DbSet<CommunicationRecipient> CommunicationRecipients => Set<CommunicationRecipient>();

    public DbSet<MembershipPayment> MembershipPayments => Set<MembershipPayment>();

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<QrCheckInToken> QrCheckInTokens => Set<QrCheckInToken>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<FitnessProfile> FitnessProfiles => Set<FitnessProfile>();

    public DbSet<FitnessGoal> FitnessGoals => Set<FitnessGoal>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    public DbSet<MemberProgressAssessment> MemberProgressAssessments => Set<MemberProgressAssessment>();

    public DbSet<MemberProgressPhoto> MemberProgressPhotos => Set<MemberProgressPhoto>();

    public DbSet<BodyScanRecord> BodyScanRecords => Set<BodyScanRecord>();

    public DbSet<WorkoutProgram> WorkoutPrograms => Set<WorkoutProgram>();

    public DbSet<WorkoutProgramDay> WorkoutProgramDays => Set<WorkoutProgramDay>();

    public DbSet<WorkoutProgramExercise> WorkoutProgramExercises => Set<WorkoutProgramExercise>();

    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

    public DbSet<WorkoutSessionExercise> WorkoutSessionExercises => Set<WorkoutSessionExercise>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<NutritionProfile> NutritionProfiles => Set<NutritionProfile>();

    public DbSet<NutritionLog> NutritionLogs => Set<NutritionLog>();

    public DbSet<AiConversation> AiConversations => Set<AiConversation>();

    public DbSet<AiMessage> AiMessages => Set<AiMessage>();

    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public DbSet<ChallengeParticipant> ChallengeParticipants => Set<ChallengeParticipant>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<LifeFitAI.Domain.Chat.ChatPermissionRule> ChatPermissionRules => Set<LifeFitAI.Domain.Chat.ChatPermissionRule>();

    public DbSet<LifeFitAI.Domain.Chat.ChatConversation> ChatConversations => Set<LifeFitAI.Domain.Chat.ChatConversation>();

    public DbSet<LifeFitAI.Domain.Chat.ChatParticipant> ChatParticipants => Set<LifeFitAI.Domain.Chat.ChatParticipant>();

    public DbSet<LifeFitAI.Domain.Chat.ChatMessage> ChatMessages => Set<LifeFitAI.Domain.Chat.ChatMessage>();

    public DbSet<LifeFitAI.Domain.Chat.ChatAttachment> ChatAttachments => Set<LifeFitAI.Domain.Chat.ChatAttachment>();

    public DbSet<LlmConnection> LlmConnections => Set<LlmConnection>();

    public DbSet<LlmModel> LlmModels => Set<LlmModel>();

    public DbSet<EmailProviderConfiguration> EmailProviderConfigurations => Set<EmailProviderConfiguration>();

    public DbSet<EmailDeliveryLog> EmailDeliveryLogs => Set<EmailDeliveryLog>();

    public DbSet<TenantStorageConnection> TenantStorageConnections => Set<TenantStorageConnection>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<StorageOAuthState> StorageOAuthStates => Set<StorageOAuthState>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
