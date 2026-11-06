using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeFitAI.Infrastructure.Persistence.Configurations;

public sealed class StaffLeaveRequestConfiguration : IEntityTypeConfiguration<StaffLeaveRequest>
{
    public void Configure(EntityTypeBuilder<StaffLeaveRequest> builder)
    {
        builder.ToTable("StaffLeaveRequests", "app");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.LeaveType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.DecisionComment).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasQueryFilter(item => item.DeletedAt == null);
    }
}
