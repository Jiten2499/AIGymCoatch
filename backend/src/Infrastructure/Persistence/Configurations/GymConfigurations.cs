using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.ToTable("BusinessProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.LegalName).HasMaxLength(200);
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.Phone).HasMaxLength(32);
        builder.Property(entity => entity.Website).HasMaxLength(500);
        builder.Property(entity => entity.Address).HasMaxLength(300);
        builder.Property(entity => entity.City).HasMaxLength(100);
        builder.Property(entity => entity.State).HasMaxLength(100);
        builder.Property(entity => entity.Country).HasMaxLength(100);
        builder.Property(entity => entity.PostalCode).HasMaxLength(20);
        builder.Property(entity => entity.LogoUrl).HasMaxLength(500);
        builder.Property(entity => entity.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Code).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Address).HasMaxLength(300);
        builder.Property(entity => entity.City).HasMaxLength(100);
        builder.Property(entity => entity.State).HasMaxLength(100);
        builder.Property(entity => entity.Country).HasMaxLength(100);
        builder.Property(entity => entity.PostalCode).HasMaxLength(20);
        builder.Property(entity => entity.Phone).HasMaxLength(32);
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("StaffProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EmployeeCode).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.StaffType).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class TrainerProfileConfiguration : IEntityTypeConfiguration<TrainerProfile>
{
    public void Configure(EntityTypeBuilder<TrainerProfile> builder)
    {
        builder.ToTable("TrainerProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Bio).HasMaxLength(2000);
        builder.Property(entity => entity.Specialization).HasMaxLength(200);
        builder.Property(entity => entity.Certification).HasMaxLength(500);
        builder.Property(entity => entity.ProfileImageUrl).HasMaxLength(500);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}

public sealed class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder.ToTable("MemberProfiles", "app");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MemberCode).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Gender).HasMaxLength(32);
        builder.Property(entity => entity.EmergencyContactName).HasMaxLength(200);
        builder.Property(entity => entity.EmergencyContactPhone).HasMaxLength(32);
        builder.Property(entity => entity.Address).HasMaxLength(300);
        builder.Property(entity => entity.City).HasMaxLength(100);
        builder.Property(entity => entity.State).HasMaxLength(100);
        builder.Property(entity => entity.PostalCode).HasMaxLength(20);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entity => entity.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(entity => entity.DeletedAt == null);
    }
}
