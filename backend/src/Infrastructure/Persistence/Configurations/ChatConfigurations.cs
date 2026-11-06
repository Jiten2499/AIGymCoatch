using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class ChatPermissionRuleConfiguration : IEntityTypeConfiguration<ChatPermissionRule>
{
    public void Configure(EntityTypeBuilder<ChatPermissionRule> builder)
    {
        builder.ToTable("ChatPermissionRules", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RuleCode).HasMaxLength(64);
        builder.Property(entity => entity.Scope).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("ChatConversations", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ConversationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.PairKey).HasMaxLength(80);
        builder.Property(entity => entity.LastPreview).HasMaxLength(200);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class ChatParticipantConfiguration : IEntityTypeConfiguration<ChatParticipant>
{
    public void Configure(EntityTypeBuilder<ChatParticipant> builder)
    {
        builder.ToTable("ChatParticipants", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MessageType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Body).HasMaxLength(4000);
        builder.Property(entity => entity.ClientMessageId).HasMaxLength(64);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.ToTable("ChatAttachments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.StorageKey).HasMaxLength(500);
        builder.Property(entity => entity.FileName).HasMaxLength(200);
        builder.Property(entity => entity.ContentType).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(entity => entity.StoredFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
