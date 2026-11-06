using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class StaffCompensationConfiguration : IEntityTypeConfiguration<StaffCompensation>
{
    public void Configure(EntityTypeBuilder<StaffCompensation> builder)
    {
        builder.ToTable("StaffCompensation", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.BaseSalary).HasPrecision(19, 4);
        builder.Property(item => item.Allowances).HasPrecision(19, 4);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffProfile>().WithMany().HasForeignKey(item => item.StaffProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("PayrollRuns", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> builder)
    {
        builder.ToTable("PayrollLines", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EmployeeName).HasMaxLength(200);
        builder.Property(item => item.EmployeeCode).HasMaxLength(32);
        builder.Property(item => item.StaffType).HasMaxLength(32);
        builder.Property(item => item.BaseSalary).HasPrecision(19, 4);
        builder.Property(item => item.Allowances).HasPrecision(19, 4);
        builder.Property(item => item.Commissions).HasPrecision(19, 4);
        builder.Property(item => item.Bonuses).HasPrecision(19, 4);
        builder.Property(item => item.Deductions).HasPrecision(19, 4);
        builder.Property(item => item.GrossPay).HasPrecision(19, 4);
        builder.Property(item => item.NetPay).HasPrecision(19, 4);
        builder.Property(item => item.AmountPaid).HasPrecision(19, 4);
        builder.Property(item => item.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PayrollRunId, item.StaffProfileId }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollRun>().WithMany().HasForeignKey(item => item.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffProfile>().WithMany().HasForeignKey(item => item.StaffProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
    {
        builder.ToTable("PayrollAdjustments", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Label).HasMaxLength(100);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollLine>().WithMany().HasForeignKey(item => item.PayrollLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}

public sealed class PayrollPaymentConfiguration : IEntityTypeConfiguration<PayrollPayment>
{
    public void Configure(EntityTypeBuilder<PayrollPayment> builder)
    {
        builder.ToTable("PayrollPayments", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reference).HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollLine>().WithMany().HasForeignKey(item => item.PayrollLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}
