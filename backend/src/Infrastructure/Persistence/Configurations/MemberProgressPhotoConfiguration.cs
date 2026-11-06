using LifeFitAI.Domain.Fitness;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class MemberProgressPhotoConfiguration : IEntityTypeConfiguration<MemberProgressPhoto>
{
    public void Configure(EntityTypeBuilder<MemberProgressPhoto> builder)
    {
        builder.ToTable("MemberProgressPhotos", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.StorageKey).HasMaxLength(200);
        builder.Property(entity => entity.ContentType).HasMaxLength(80);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProgressAssessment>().WithMany().HasForeignKey(entity => entity.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
