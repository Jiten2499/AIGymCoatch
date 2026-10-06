using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class NutritionProfileConfiguration : IEntityTypeConfiguration<NutritionProfile>
{
    public void Configure(EntityTypeBuilder<NutritionProfile> builder)
    {
        builder.ToTable("NutritionProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DailyCalorieTarget).HasPrecision(8, 2);
        builder.Property(entity => entity.ProteinTarget).HasPrecision(8, 2);
        builder.Property(entity => entity.CarbTarget).HasPrecision(8, 2);
        builder.Property(entity => entity.FatTarget).HasPrecision(8, 2);
        builder.Property(entity => entity.DietaryPreference).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class NutritionLogConfiguration : IEntityTypeConfiguration<NutritionLog>
{
    public void Configure(EntityTypeBuilder<NutritionLog> builder)
    {
        builder.ToTable("NutritionLogs", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MealType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.FoodName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Quantity).HasPrecision(8, 2);
        builder.Property(entity => entity.Unit).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Calories).HasPrecision(8, 2);
        builder.Property(entity => entity.Protein).HasPrecision(8, 2);
        builder.Property(entity => entity.Carbohydrates).HasPrecision(8, 2);
        builder.Property(entity => entity.Fat).HasPrecision(8, 2);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Content).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(64);
        builder.Property(entity => entity.Model).HasMaxLength(100);
        builder.HasOne<AiConversation>().WithMany().HasForeignKey(entity => entity.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiUsageRecordConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
    {
        builder.ToTable("AiUsageRecords", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Model).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.RequestType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.EstimatedCost).HasPrecision(12, 6);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.ToTable("Challenges", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.ChallengeType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TargetValue).HasPrecision(12, 2);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class ChallengeParticipantConfiguration : IEntityTypeConfiguration<ChallengeParticipant>
{
    public void Configure(EntityTypeBuilder<ChallengeParticipant> builder)
    {
        builder.ToTable("ChallengeParticipants", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CurrentValue).HasPrecision(12, 2);
        builder.HasOne<Challenge>().WithMany().HasForeignKey(entity => entity.ChallengeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(1000).IsRequired();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
