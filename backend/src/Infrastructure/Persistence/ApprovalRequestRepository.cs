using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Approvals;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class ApprovalRequestRepository : IApprovalRequestRepository
{
    private readonly ApplicationDbContext _db;

    public ApprovalRequestRepository(ApplicationDbContext db) => _db = db;

    public async Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
        await _db.ApprovalRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);

    public Task<bool> HasPendingAsync(Guid tenantId, ApprovalRequestType requestType, Guid sourceId, CancellationToken cancellationToken) =>
        _db.ApprovalRequests.AnyAsync(
            item => item.TenantId == tenantId && item.RequestType == requestType && item.SourceId == sourceId && item.Status == ApprovalRequestStatus.Pending,
            cancellationToken);

    public Task<ApprovalRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.ApprovalRequests.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<PageResult<ApprovalRequestListItem>> SearchAsync(Guid tenantId, IReadOnlyList<string> permissions, ApprovalListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var size = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var rows = Filter(_db.ApprovalRequests.AsNoTracking(), tenantId, permissions, query);
        var ordered = query.Sort switch
        {
            "createdAt" => rows.OrderBy(item => item.CreatedAt),
            "requestedBy" => rows.OrderBy(item => item.RequestedByName),
            _ => rows.OrderByDescending(item => item.CreatedAt),
        };
        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await ordered.Skip((page - 1) * size).Take(size)
            .Select(item => new ApprovalRequestListItem(item.Id, item.RequestType, item.Title, item.Summary, item.RequestedByName, item.CreatedAt, item.RelatedLabel, item.Amount, item.Currency, item.Days, item.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageResult<ApprovalRequestListItem>(items, page, size, total);
    }

    public Task<int> CountPendingAsync(Guid tenantId, IReadOnlyList<string> permissions, CancellationToken cancellationToken) =>
        _db.ApprovalRequests.CountAsync(
            item => item.TenantId == tenantId && item.Status == ApprovalRequestStatus.Pending && permissions.Contains(item.RequiredPermission),
            cancellationToken);

    private static IQueryable<ApprovalRequest> Filter(IQueryable<ApprovalRequest> rows, Guid tenantId, IReadOnlyList<string> permissions, ApprovalListQuery query)
    {
        var from = query.From?.ToDateTime(TimeOnly.MinValue);
        var to = query.To?.ToDateTime(TimeOnly.MaxValue);
        return rows.Where(item =>
            item.TenantId == tenantId
            && permissions.Contains(item.RequiredPermission)
            && (query.Status == null || item.Status == query.Status)
            && (query.RequestType == null || item.RequestType == query.RequestType)
            && (query.RequestedByUserId == null || item.RequestedByUserId == query.RequestedByUserId)
            && (from == null || item.CreatedAt >= from)
            && (to == null || item.CreatedAt <= to));
    }
}
