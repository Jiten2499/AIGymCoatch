using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RequestType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RequiredPermission).HasMaxLength(64);
        builder.Property(entity => entity.Title).HasMaxLength(200);
        builder.Property(entity => entity.Summary).HasMaxLength(500);
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.Property(entity => entity.DecisionComment).HasMaxLength(500);
        builder.Property(entity => entity.RequestedByName).HasMaxLength(200);
        builder.Property(entity => entity.DecidedByName).HasMaxLength(200);
        builder.Property(entity => entity.RelatedLabel).HasMaxLength(200);
        builder.Property(entity => entity.Amount).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.DecidedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
