using LifeFitAI.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class LlmConnectionConfiguration : IEntityTypeConfiguration<LlmConnection>
{
    public void Configure(EntityTypeBuilder<LlmConnection> builder)
    {
        builder.ToTable("LlmConnections", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.ProtectedApiKey).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.DefaultModel).HasMaxLength(200);
        builder.Property(entity => entity.HealthStatus).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> builder)
    {
        builder.ToTable("LlmModels", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ModelIdentifier).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
