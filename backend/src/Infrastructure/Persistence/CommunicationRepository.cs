using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Communications;
using LifeFitAI.Domain.Gym;
using LifeFitAI.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class CommunicationRepository : ICommunicationRepository
{
    private readonly ApplicationDbContext _db;

    public CommunicationRepository(ApplicationDbContext db) => _db = db;

    public Task AddAsync(Communication communication, CancellationToken cancellationToken) =>
        _db.Communications.AddAsync(communication, cancellationToken).AsTask();

    public Task<Communication?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.Communications.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);

    public async Task ReplaceAudienceAsync(Guid tenantId, Guid communicationId, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken)
    {
        var existing = await _db.CommunicationAudienceMembers
            .Where(item => item.TenantId == tenantId && item.CommunicationId == communicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.CommunicationAudienceMembers.RemoveRange(existing);
        foreach (var memberId in memberIds.Distinct())
        {
            await _db.CommunicationAudienceMembers.AddAsync(
                CommunicationAudienceMember.Create(Guid.NewGuid(), tenantId, communicationId, memberId),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SoftDeleteAudienceAsync(Guid tenantId, Guid communicationId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var existing = await _db.CommunicationAudienceMembers
            .Where(item => item.TenantId == tenantId && item.CommunicationId == communicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in existing)
        {
            row.SoftDelete(utcNow);
        }
    }

    public async Task<IReadOnlyList<Guid>> AudienceMemberIdsAsync(Guid tenantId, Guid communicationId, CancellationToken cancellationToken) =>
        await _db.CommunicationAudienceMembers.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.CommunicationId == communicationId)
            .Select(item => item.MemberProfileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<PageResult<CommunicationListItem>> SearchAsync(Guid tenantId, CommunicationSearch search, CancellationToken cancellationToken)
    {
        var query = from item in _db.Communications.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on item.CreatedByUserId equals user.Id
                    where item.TenantId == tenantId
                    select new { item, Creator = user.FirstName + " " + user.LastName };

        if (!string.IsNullOrWhiteSpace(search.Term))
        {
            var term = search.Term.Trim();
            query = query.Where(row => row.item.Title.Contains(term) || row.item.Message.Contains(term));
        }

        if (search.Type is CommunicationType type)
        {
            query = query.Where(row => row.item.Type == type);
        }

        if (search.Status is CommunicationStatus status)
        {
            query = query.Where(row => row.item.Status == status);
        }

        if (search.AudienceType is CommunicationAudienceType audience)
        {
            query = query.Where(row => row.item.AudienceType == audience);
        }

        if (search.PublishFrom is DateTime from)
        {
            query = query.Where(row => row.item.PublishAt != null && row.item.PublishAt >= from);
        }

        if (search.PublishTo is DateTime to)
        {
            query = query.Where(row => row.item.PublishAt != null && row.item.PublishAt <= to);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = new PageQuery(search.Page, search.PageSize);
        var items = await query
            .OrderByDescending(row => row.item.CreatedAt)
            .Skip(page.Skip)
            .Take(page.NormalizedSize)
            .Select(row => new CommunicationListItem(
                row.item.Id,
                row.item.Type,
                row.item.Title,
                row.item.Priority,
                row.item.Status,
                row.item.AudienceType,
                row.item.PublishAt,
                row.item.ExpiresAt,
                row.Creator,
                row.item.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PageResult<CommunicationListItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<CommunicationDetails?> ReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var row = await (from item in _db.Communications.AsNoTracking()
                         join user in _db.Users.AsNoTracking() on item.CreatedByUserId equals user.Id
                         where item.TenantId == tenantId && item.Id == id
                         select new { item, Creator = user.FirstName + " " + user.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var selected = await AudienceMemberIdsAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        return new CommunicationDetails(
            row.item.Id,
            row.item.Type,
            row.item.Title,
            row.item.Message,
            row.item.Priority,
            row.item.Status,
            row.item.AudienceType,
            row.item.AudiencePackageId,
            row.item.AudienceTrainerId,
            row.item.ExpiringWithinDays,
            selected,
            row.item.ImageStorageKey,
            row.item.PublishAt,
            row.item.ExpiresAt,
            row.item.CreatedByUserId,
            row.Creator,
            row.item.CreatedAt,
            row.item.PublishedAt,
            row.item.ArchivedAt);
    }

    public async Task<bool> IsAudienceMatchAsync(
        Guid tenantId,
        Communication communication,
        Guid memberProfileId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == memberProfileId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return false;
        }

        return communication.AudienceType switch
        {
            CommunicationAudienceType.AllMembers => true,
            CommunicationAudienceType.ActiveMembers => member.Status == ProfileStatus.Active,
            CommunicationAudienceType.ExpiringMembers => await HasExpiringMembershipAsync(tenantId, memberProfileId, communication.ExpiringWithinDays ?? 7, utcNow, cancellationToken).ConfigureAwait(false),
            CommunicationAudienceType.MembershipPackage => communication.AudiencePackageId is Guid packageId
                && await HasPackageMembershipAsync(tenantId, memberProfileId, packageId, utcNow, cancellationToken).ConfigureAwait(false),
            CommunicationAudienceType.TrainerMembers => communication.AudienceTrainerId is Guid trainerId
                && member.AssignedTrainerId == trainerId,
            CommunicationAudienceType.SelectedMembers => await _db.CommunicationAudienceMembers.AsNoTracking()
                .AnyAsync(item => item.TenantId == tenantId && item.CommunicationId == communication.Id && item.MemberProfileId == memberProfileId, cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    public async Task<PageResult<MemberCommunicationItem>> ListForMemberAsync(
        Guid tenantId,
        Guid memberProfileId,
        Guid userId,
        CommunicationType? type,
        int page,
        int pageSize,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var query = _db.Communications.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.Status == CommunicationStatus.Published
                && item.PublishAt != null
                && item.PublishAt <= utcNow
                && (item.ExpiresAt == null || item.ExpiresAt > utcNow));
        if (type is CommunicationType filter)
        {
            query = query.Where(item => item.Type == filter);
        }

        var candidates = await query.OrderByDescending(item => item.PublishAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        var matched = new List<Communication>();
        foreach (var item in candidates)
        {
            if (await IsAudienceMatchAsync(tenantId, item, memberProfileId, utcNow, cancellationToken).ConfigureAwait(false))
            {
                matched.Add(item);
            }
        }

        var pageQuery = new PageQuery(page, pageSize);
        var slice = matched.Skip(pageQuery.Skip).Take(pageQuery.NormalizedSize).ToList();
        var ids = slice.Select(item => item.Id).ToList();
        var reads = await _db.CommunicationReads.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && ids.Contains(item.CommunicationId))
            .Select(item => item.CommunicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var readSet = reads.ToHashSet();
        var creatorIds = slice.Select(item => item.CreatedByUserId).Distinct().ToList();
        var creators = await _db.Users.AsNoTracking()
            .Where(user => creatorIds.Contains(user.Id))
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken)
            .ConfigureAwait(false);
        var items = slice.Select(item => new MemberCommunicationItem(
            item.Id,
            item.Type,
            item.Title,
            item.Message,
            item.Priority,
            item.PublishAt!.Value,
            item.ExpiresAt,
            readSet.Contains(item.Id),
            item.ImageStorageKey,
            creators.GetValueOrDefault(item.CreatedByUserId))).ToList();
        return new PageResult<MemberCommunicationItem>(items, pageQuery.NormalizedPage, pageQuery.NormalizedSize, matched.Count);
    }

    public Task MarkReadAsync(CommunicationRead read, CancellationToken cancellationToken) =>
        _db.CommunicationReads.AddAsync(read, cancellationToken).AsTask();

    public Task<bool> HasReadAsync(Guid tenantId, Guid communicationId, Guid userId, CancellationToken cancellationToken) =>
        _db.CommunicationReads.AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.CommunicationId == communicationId && item.UserId == userId, cancellationToken);

    private async Task<bool> HasExpiringMembershipAsync(Guid tenantId, Guid memberProfileId, int days, DateTime utcNow, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        var until = today.AddDays(days);
        return await _db.GymMemberships.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId
                && item.MemberProfileId == memberProfileId
                && (item.Status == GymMembershipStatus.Active || item.Status == GymMembershipStatus.Frozen)
                && item.EndDate >= today
                && item.EndDate <= until,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> HasPackageMembershipAsync(Guid tenantId, Guid memberProfileId, Guid packageId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        return _db.GymMemberships.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId
                && item.MemberProfileId == memberProfileId
                && item.MembershipPlanId == packageId
                && (item.Status == GymMembershipStatus.Active || item.Status == GymMembershipStatus.Frozen || item.Status == GymMembershipStatus.Pending)
                && item.EndDate >= today,
            cancellationToken);
    }
}
