using LifeFitAI.Application.Gym;
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

    public async Task AddStaffAsync(StaffProfile profile, CancellationToken cancellationToken) =>
        await _db.StaffProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

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

    public async Task<PageResult<TrainerResponse>> ListTrainersAsync(Guid tenantId, PageQuery page, CancellationToken cancellationToken)
    {
        var query = from profile in _db.TrainerProfiles.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where profile.TenantId == tenantId
                    orderby user.LastName
                    select new TrainerResponse(profile.Id, profile.UserId, user.Email, user.FirstName, user.LastName, profile.Bio, profile.Specialization, profile.ExperienceYears, profile.Certification, profile.ProfileImageUrl, profile.BranchId, profile.Status);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Skip(page.Skip).Take(page.NormalizedSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<TrainerResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }

    public Task<MemberProfile?> GetMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _db.MemberProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.Id == id, cancellationToken);

    public Task<MemberProfile?> GetMemberByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
        _db.MemberProfiles.SingleOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);

    public async Task AddMemberAsync(MemberProfile profile, CancellationToken cancellationToken) =>
        await _db.MemberProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);

    public async Task<PageResult<MemberResponse>> SearchMembersAsync(
        Guid tenantId,
        string? term,
        ProfileStatus? status,
        Guid? branchId,
        GymMembershipStatus? membershipStatus,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var query = from profile in _db.MemberProfiles.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on profile.UserId equals user.Id
                    where profile.TenantId == tenantId
                    select new { profile, user };

        if (status is not null)
        {
            query = query.Where(row => row.profile.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();
            query = query.Where(row =>
                row.profile.MemberCode.StartsWith(value)
                || row.user.FirstName.StartsWith(value)
                || row.user.LastName.StartsWith(value)
                || row.user.Email.StartsWith(value)
                || (row.user.Phone != null && row.user.Phone.StartsWith(value))
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
            "name" => query.OrderBy(row => row.user.LastName).ThenBy(row => row.user.FirstName),
            _ => query.OrderByDescending(row => row.profile.JoinDate),
        };

        var total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await ordered.Skip(page.Skip).Take(page.NormalizedSize)
            .Select(row => new MemberResponse(
                row.profile.Id, row.profile.UserId, row.profile.MemberCode, row.user.Email, row.user.FirstName, row.user.LastName, row.user.Phone,
                row.profile.DateOfBirth, row.profile.Gender, row.profile.EmergencyContactName, row.profile.EmergencyContactPhone,
                row.profile.Address, row.profile.City, row.profile.State, row.profile.PostalCode, row.profile.JoinDate, row.profile.Status))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PageResult<MemberResponse>(items, page.NormalizedPage, page.NormalizedSize, total);
    }
}
