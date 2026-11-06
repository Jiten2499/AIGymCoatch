using LifeFitAI.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class EmailProviderConfigurationMap : IEntityTypeConfiguration<EmailProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<EmailProviderConfiguration> builder)
    {
        builder.ToTable("EmailProviderConfigurations", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.SmtpHost).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.Encryption).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Username).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.ProtectedPassword).HasMaxLength(4000);
        builder.Property(entity => entity.SenderName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.SenderEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.ReplyToEmail).HasMaxLength(320);
        builder.Property(entity => entity.LastTestStatus).HasMaxLength(32);
        builder.Property(entity => entity.LastTestMessage).HasMaxLength(500);
        builder.Property(entity => entity.LastEmailStatus).HasMaxLength(32);
        builder.Property(entity => entity.HealthStatus).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class EmailDeliveryLogConfiguration : IEntityTypeConfiguration<EmailDeliveryLog>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryLog> builder)
    {
        builder.ToTable("EmailDeliveryLogs", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.Subject).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.TemplateKey).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Classification).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.ProviderMessageId).HasMaxLength(200);
        builder.Property(entity => entity.FailureReason).HasMaxLength(500);
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
