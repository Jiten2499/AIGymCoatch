using LifeFitAI.Application.Abstractions.Persistence;
using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
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

    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    public DbSet<GymMembership> GymMemberships => Set<GymMembership>();

    public DbSet<MembershipHistoryEntry> MembershipHistory => Set<MembershipHistoryEntry>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<QrCheckInToken> QrCheckInTokens => Set<QrCheckInToken>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<FitnessProfile> FitnessProfiles => Set<FitnessProfile>();

    public DbSet<FitnessGoal> FitnessGoals => Set<FitnessGoal>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

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

    public DbSet<LlmConnection> LlmConnections => Set<LlmConnection>();

    public DbSet<LlmModel> LlmModels => Set<LlmModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
