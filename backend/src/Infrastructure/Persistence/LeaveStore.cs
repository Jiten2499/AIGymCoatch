using LifeFitAI.Application.Gym.Leave;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class LeaveStore : ILeaveStore
{
    private readonly ApplicationDbContext _db;

    public LeaveStore(ApplicationDbContext db) => _db = db;

    public Task AddAsync(StaffLeaveRequest request, CancellationToken cancellationToken)
    {
        _db.StaffLeaveRequests.Add(request);
        return Task.CompletedTask;
    }

    public Task<StaffLeaveRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.StaffLeaveRequests.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public Task<StaffLeaveRequest?> GetByApprovalAsync(Guid tenantId, Guid approvalRequestId, CancellationToken cancellationToken) =>
        _db.StaffLeaveRequests.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ApprovalRequestId == approvalRequestId, cancellationToken);

    public async Task<IReadOnlyList<StaffLeaveRequest>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        await _db.StaffLeaveRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderByDescending(item => item.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<StaffLeaveRequest>> SearchAsync(Guid tenantId, StaffLeaveCalendarQuery query, CancellationToken cancellationToken)
    {
        var rows = _db.StaffLeaveRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.StartDate <= query.To && item.EndDate >= query.From);
        if (query.StaffProfileId is Guid staffId)
        {
            rows = rows.Where(item => item.StaffProfileId == staffId);
        }

        if (query.LeaveType is LeaveType leaveType)
        {
            rows = rows.Where(item => item.LeaveType == leaveType);
        }

        if (query.Status is LeaveRequestStatus status)
        {
            rows = rows.Where(item => item.Status == status);
        }

        return await rows.OrderBy(item => item.StartDate).ThenBy(item => item.StaffProfileId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasOverlapAsync(Guid tenantId, Guid staffProfileId, DateOnly start, DateOnly end, Guid? excludeId, CancellationToken cancellationToken)
    {
        var rows = await _db.StaffLeaveRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.StaffProfileId == staffProfileId
                && (item.Status == LeaveRequestStatus.Pending || item.Status == LeaveRequestStatus.Approved)
                && item.StartDate <= end
                && item.EndDate >= start
                && (excludeId == null || item.Id != excludeId))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows;
    }
}
