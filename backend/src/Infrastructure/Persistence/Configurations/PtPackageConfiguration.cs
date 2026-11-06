using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class PtPackageConfiguration : IEntityTypeConfiguration<PtPackage>
{
    public void Configure(EntityTypeBuilder<PtPackage> builder)
    {
        builder.ToTable("PtPackages", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Price).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).HasColumnName("CatalogueStatus");
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class MemberPtAssignmentConfiguration : IEntityTypeConfiguration<MemberPtAssignment>
{
    public void Configure(EntityTypeBuilder<MemberPtAssignment> builder)
    {
        builder.ToTable("MemberPtAssignments", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Price).HasPrecision(19, 4);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.PackageName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CancellationReason).HasMaxLength(500);
        builder.Property(entity => entity.PaymentStatus).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PtPackage>().WithMany().HasForeignKey(entity => entity.PtPackageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerProfile>().WithMany().HasForeignKey(entity => entity.TrainerProfileId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class PtSessionConfiguration : IEntityTypeConfiguration<PtSession>
{
    public void Configure(EntityTypeBuilder<PtSession> builder)
    {
        builder.ToTable("PtSessions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.Location).HasMaxLength(120);
        builder.Property(entity => entity.SessionType).HasMaxLength(80);
        builder.Property(entity => entity.Focus).HasMaxLength(500);
        builder.Property(entity => entity.CompletedOn).IsRequired(false);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany().HasForeignKey(entity => entity.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerProfile>().WithMany().HasForeignKey(entity => entity.TrainerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class PtSessionConsumptionConfiguration : IEntityTypeConfiguration<PtSessionConsumption>
{
    public void Configure(EntityTypeBuilder<PtSessionConsumption> builder)
    {
        builder.ToTable("PtSessionConsumptions", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(entity => entity.ReversalReason).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PtSession>().WithMany().HasForeignKey(entity => entity.PtSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GymMembership>().WithMany().HasForeignKey(entity => entity.GymMembershipId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberPtAssignment>().WithMany().HasForeignKey(entity => entity.MemberPtAssignmentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class PtSessionExerciseConfiguration : IEntityTypeConfiguration<PtSessionExercise>
{
    public void Configure(EntityTypeBuilder<PtSessionExercise> builder)
    {
        builder.ToTable("PtSessionExercises", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.MuscleGroup).HasMaxLength(80);
        builder.Property(entity => entity.Reps).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PtSession>().WithMany().HasForeignKey(entity => entity.PtSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
