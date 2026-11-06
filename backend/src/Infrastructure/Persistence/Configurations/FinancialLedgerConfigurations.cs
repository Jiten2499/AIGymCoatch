using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.AccountClass).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable("FinancialAccounts", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.Property(item => item.AccountKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(item => item.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Description).HasMaxLength(500).IsRequired();
        builder.Property(item => item.EntryType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.SourceType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Category).HasMaxLength(100);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(item => item.FinancialAccountId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(item => item.CounterFinancialAccountId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.VoidedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(item => item.ReversalOfJournalEntryId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLines", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DebitAmount).HasPrecision(19, 4);
        builder.Property(item => item.CreditAmount).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(item => item.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(item => item.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}
