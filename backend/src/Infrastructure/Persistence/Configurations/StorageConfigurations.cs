using LifeFitAI.Domain.Identity;
using LifeFitAI.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class TenantStorageConnectionConfiguration : IEntityTypeConfiguration<TenantStorageConnection>
{
    public void Configure(EntityTypeBuilder<TenantStorageConnection> builder)
    {
        builder.ToTable("TenantStorageConnections", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.AccountEmail).HasMaxLength(320);
        builder.Property(item => item.RootFolderId).HasMaxLength(128);
        builder.Property(item => item.MembersFolderId).HasMaxLength(128);
        builder.Property(item => item.CommunicationsFolderId).HasMaxLength(128);
        builder.Property(item => item.GymFolderId).HasMaxLength(128);
        builder.Property(item => item.OtherFolderId).HasMaxLength(128);
        builder.Property(item => item.ProtectedRefreshToken);
        builder.Property(item => item.GrantedScopes).HasMaxLength(500);
        builder.Property(item => item.LastError).HasMaxLength(500);
        builder.Property(item => item.ProgressFolderId).HasMaxLength(128);
        builder.Property(item => item.ProgressFolderName).HasMaxLength(200);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.ConnectedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.ExternalFileId).HasMaxLength(128).IsRequired();
        builder.Property(item => item.ParentFolderId).HasMaxLength(128).IsRequired();
        builder.Property(item => item.FileName).HasMaxLength(255).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(item => item.LogicalKey).HasMaxLength(500).IsRequired();
        builder.Property(item => item.EntityType).HasMaxLength(64);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class StorageOAuthStateConfiguration : IEntityTypeConfiguration<StorageOAuthState>
{
    public void Configure(EntityTypeBuilder<StorageOAuthState> builder)
    {
        builder.ToTable("StorageOAuthStates", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StateHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.CodeVerifier).HasMaxLength(128).IsRequired();
        builder.Property(item => item.ClientKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.ReturnPath).HasMaxLength(500);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
