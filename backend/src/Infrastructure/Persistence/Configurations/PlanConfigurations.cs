using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class PlanDefinitionConfiguration : IEntityTypeConfiguration<PlanDefinition>
{
    public void Configure(EntityTypeBuilder<PlanDefinition> builder)
    {
        builder.ToTable("Plans", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.AppliesTo).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class FeatureDefinitionConfiguration : IEntityTypeConfiguration<FeatureDefinition>
{
    public void Configure(EntityTypeBuilder<FeatureDefinition> builder)
    {
        builder.ToTable("FeatureDefinitions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Category).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Scope).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("PlanFeatures", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FeatureCode).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Enabled).HasColumnName("Enabled");
        builder.Property(entity => entity.RowVersion).IsRowVersion();
    }
}
