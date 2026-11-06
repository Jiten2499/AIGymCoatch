using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Payments;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class MembershipPaymentRepository : IMembershipPaymentRepository
{
    private readonly ApplicationDbContext _db;

    public MembershipPaymentRepository(ApplicationDbContext db) => _db = db;

    public Task AddAsync(MembershipPayment payment, CancellationToken cancellationToken) =>
        _db.MembershipPayments.AddAsync(payment, cancellationToken).AsTask();

    public Task<MembershipPayment?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MembershipPayments.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task<MembershipPaymentItem?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var row = await (from payment in _db.MembershipPayments.AsNoTracking()
                         join user in _db.Users.AsNoTracking() on payment.ReceivedByUserId equals user.Id
                         where payment.TenantId == tenantId && payment.Id == id
                         select new { payment, Receiver = user.FirstName + " " + user.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Map(row.payment, row.Receiver);
    }

    public async Task<decimal> SumPostedAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken) =>
        await _db.MembershipPayments.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.GymMembershipId == membershipId
                && item.Status == MembershipPaymentStatus.Posted)
            .SumAsync(item => item.Amount, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PageResult<MembershipPaymentItem>> ListForMembershipAsync(
        Guid tenantId,
        Guid membershipId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = from payment in _db.MembershipPayments.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on payment.ReceivedByUserId equals user.Id
                    where payment.TenantId == tenantId && payment.GymMembershipId == membershipId
                    select new { payment, Receiver = user.FirstName + " " + user.LastName };
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var pageQuery = new PageQuery(page, pageSize);
        var items = await query
            .OrderByDescending(row => row.payment.PaymentDate)
            .ThenByDescending(row => row.payment.CreatedAt)
            .Skip(pageQuery.Skip)
            .Take(pageQuery.NormalizedSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageResult<MembershipPaymentItem>(items.Select(row => Map(row.payment, row.Receiver)).ToList(), pageQuery.NormalizedPage, pageQuery.NormalizedSize, total);
    }

    private static MembershipPaymentItem Map(MembershipPayment payment, string receiver) =>
        new(
            payment.Id,
            payment.MemberProfileId,
            payment.GymMembershipId,
            payment.Amount,
            payment.Currency,
            payment.PaymentMethod,
            payment.PaymentDate,
            payment.ReferenceNumber,
            payment.Notes,
            payment.Status,
            payment.ReceivedByUserId,
            receiver,
            payment.CreatedAt,
            payment.VoidedAt);
}
