using LifeFitAI.Application.Gym;
using LifeFitAI.Application.Gym.Members;
using LifeFitAI.Domain.Gym;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed class PeopleRepository : IPeopleRepository
{
    private readonly ApplicationDbContext _db;

    public PeopleRepository(ApplicationDbContext db) => _db = db;

    public Task<StaffProfile?> GetStaffAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.StaffProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.Id == id, cancellationToken);

    public Task<StaffProfile?> GetStaffByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.StaffProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken, Guid? excludeStaffId = null) =>
        _db.StaffProfiles.AnyAsync(
            profile => profile.TenantId == tenantId
                && profile.EmployeeCode == employeeCode
                && (excludeStaffId == null || profile.Id != excludeStaffId),
            cancellationToken);

    public async Task AddStaffAsync(StaffProfile profile, CancellationToken cancellationToken) =>
        await _db.StaffProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

    public async Task AddStaffBranchAccessAsync(StaffBranchAccess access, CancellationToken cancellationToken) =>
        await _db.StaffBranchAccess.AddAsync(access, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListStaffBranchIdsAsync(Guid tenantId, Guid staffProfileId, CancellationToken cancellationToken) =>
        await _db.StaffBranchAccess.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.StaffProfileId == staffProfileId)
            .Select(item => item.BranchId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceStaffBranchAccessAsync(Guid tenantId, Guid staffProfileId, IReadOnlyList<Guid> branchIds, DateTime utcNow, CancellationToken cancellationToken)
    {
        var existing = await _db.StaffBranchAccess
            .Where(item => item.TenantId == tenantId && item.StaffProfileId == staffProfileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in existing)
        {
            row.SoftDelete(utcNow);
        }

        foreach (var branchId in branchIds.Distinct())
        {
            await _db.StaffBranchAccess.AddAsync(StaffBranchAccess.Create(Guid.NewGuid(), tenantId, staffProfileId, branchId), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<PageResult<StaffResponse>> ListStaffAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = from profile in _db.StaffProfiles.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where profile.TenantId == tenantId
                    orderby user.LastName, user.FirstName
                    select new StaffResponse(profile.Id, profile.UserId, profile.EmployeeCode, user.Email, user.FirstName, user.LastName, profile.BranchId, profile.StaffType, profile.JoiningDate, profile.Status);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<StaffResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<TrainerProfile?> GetTrainerAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.Id == id, cancellationToken);

    public Task<TrainerProfile?> GetTrainerByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.TrainerProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public async Task AddTrainerAsync(TrainerProfile profile, CancellationToken cancellationToken) =>
        await _db.TrainerProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<TrainerResponse>> ListTrainersAsync(Guid tenantId, PageQuery page, string? search, string? status, string? specialization, CancellationToken cancellationToken)
    {
        var profiles = _db.TrainerProfiles.AsNoTracking().Where(profile => profile.TenantId == tenantId);
        if (Enum.TryParse<ProfileStatus>(status, out var parsedStatus))
        {
            profiles = profiles.Where(profile => profile.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(specialization))
        {
            var specialty = specialization.Trim();
            profiles = profiles.Where(profile => profile.Specialization != null && profile.Specialization.Contains(specialty));
        }

        var joined = from profile in profiles
                     join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
                     select new { profile, user };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            joined = joined.Where(row => row.user.FirstName.Contains(term) || row.user.LastName.Contains(term) || row.user.Email.Contains(term) || (row.profile.EmployeeCode != null && row.profile.EmployeeCode.Contains(term)));
        }

        var ordered = joined.OrderBy(row => row.user.LastName).ThenBy(row => row.user.FirstName);
        var total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        var pageRows = await ordered.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ids = pageRows.Select(row => row.profile.Id).ToArray();
        var counts = await _db.TrainerMemberAssignments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == TrainerAssignmentStatus.Active && ids.Contains(item.TrainerProfileId))
            .GroupBy(item => item.TrainerProfileId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var items = pageRows.Select(row => TrainerMaps.ToResponse(row.profile, row.user, counts.FirstOrDefault(item => item.Key == row.profile.Id)?.Count ?? 0)).ToArray();
        return new PageResult<TrainerResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<MemberProfile?> GetMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MemberProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.Id == id, cancellationToken);

    public Task<MemberProfile?> GetMemberByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.MemberProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public async Task<MemberProfile?> FindMemberWithPhotoAsync(Guid userId, Guid? preferredTenantId, CancellationToken cancellationToken)
    {
        var rows = await _db.MemberProfiles.Where(profile => profile.UserId == userId && profile.PhotoStorageKey != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (preferredTenantId is Guid tenantId)
        {
            var preferred = rows.FirstOrDefault(profile => profile.TenantId == tenantId);
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return rows.FirstOrDefault();
    }

    public async Task AddMemberAsync(MemberProfile profile, CancellationToken cancellationToken) =>
        await _db.MemberProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

    public async Task<MemberResponse?> ReadMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var row = await (
            from profile in _db.MemberProfiles.AsNoTracking()
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where profile.TenantId == tenantId && profile.Id == id
            select new { profile, user }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        string? trainerName = null;
        if (row.profile.AssignedTrainerId is Guid trainerId)
        {
            trainerName = await (
                from trainer in _db.TrainerProfiles.AsNoTracking()
                join trainerUser in _db.Users.AsNoTracking() on trainer.UserId equals trainerUser.Id
                where trainer.TenantId == tenantId && trainer.Id == trainerId
                select trainerUser.FirstName + " " + trainerUser.LastName).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return MemberMaps.From(row.profile, row.user, trainerName);
    }

    public async Task<PageResult<MemberListItem>> SearchMembersAsync(
        Guid tenantId,
        string? term,
        ProfileStatus? status,
        Guid? branchId,
        GymMembershipStatus? membershipStatus,
        PageQuery page,
        CancellationToken cancellationToken,
        Guid? onlyUserId = null,
        Guid? assignedTrainerId = null)
    {
        var query = from profile in _db.MemberProfiles.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id into users
                    from user in users.DefaultIfEmpty()
                    where profile.TenantId == tenantId
                    select new { profile, user };

        if (onlyUserId is Guid userLimit)
        {
            query = query.Where(row => row.profile.UserId == userLimit);
        }

        if (assignedTrainerId is Guid trainerLimit)
        {
            query = query.Where(row => row.profile.AssignedTrainerId == trainerLimit);
        }

        if (status is not null)
        {
            query = query.Where(row => row.profile.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();
            query = query.Where(row =>
                row.profile.MemberCode.StartsWith(value)
                || row.profile.FirstName.Contains(value)
                || row.profile.LastName.Contains(value)
                || (row.profile.FirstName + " " + row.profile.LastName).Contains(value)
                || (row.profile.Phone != null && row.profile.Phone.Contains(value))
                || (row.user != null && (row.user.FirstName.Contains(value) || row.user.LastName.Contains(value) || (row.user.FirstName + " " + row.user.LastName).Contains(value) || row.user.Email.StartsWith(value) || (row.user.Phone != null && row.user.Phone.Contains(value))))
                || _db.GymMemberships.Any(membership => membership.TenantId == tenantId && membership.MemberProfileId == row.profile.Id && membership.MembershipNumber.StartsWith(value)));
        }

        if (branchId is not null)
        {
            query = query.Where(row => _db.GymMemberships.Any(membership => membership.TenantId == tenantId && membership.MemberProfileId == row.profile.Id && membership.BranchId == branchId));
        }

        if (membershipStatus is not null)
        {
            query = query.Where(row => _db.GymMemberships.Any(membership => membership.TenantId == tenantId && membership.MemberProfileId == row.profile.Id && membership.Status == membershipStatus));
        }

        var ordered = page.Sort switch
        {
            "memberCode" => query.OrderBy(row => row.profile.MemberCode),
            "name" => query.OrderBy(row => row.profile.LastName).ThenBy(row => row.profile.FirstName),
            _ => query.OrderByDescending(row => row.profile.JoinDate),
        };

        var total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await ordered.Skip(page.Skip).Take(page.NormalizedSize)
            .Select(row => new MemberListItem(
                row.profile.Id,
                row.profile.FirstName != "" || row.profile.LastName != ""
                    ? (row.profile.FirstName + " " + row.profile.LastName).Trim()
                    : row.user == null ? row.profile.MemberCode : row.user.FirstName + " " + row.user.LastName,
                row.profile.Phone ?? (row.user == null ? null : row.user.Phone),
                row.profile.PhotoStorageKey != null,
                row.profile.Status,
                row.profile.JoinDate,
                row.user == null ? null : row.user.Email))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<MemberListItem>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public async Task<IReadOnlyList<MemberMatchItem>> FindMemberMatchesAsync(Guid tenantId, string mobile, string? email, CancellationToken cancellationToken)
    {
        var phone = mobile.Trim();
        var mail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        return await (
            from profile in _db.MemberProfiles.AsNoTracking()
            join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where profile.TenantId == tenantId
                && ((profile.Phone != null && profile.Phone == phone)
                    || (user != null && user.Phone != null && user.Phone == phone)
                    || (mail != null && user != null && user.Email == mail))
            orderby profile.JoinDate descending
            select new MemberMatchItem(
                profile.Id,
                profile.FirstName != "" || profile.LastName != ""
                    ? (profile.FirstName + " " + profile.LastName).Trim()
                    : user == null ? profile.MemberCode : user.FirstName + " " + user.LastName,
                profile.Phone ?? (user == null ? null : user.Phone),
                user == null ? null : user.Email,
                profile.MemberCode,
                profile.Status,
                profile.JoinDate))
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
