using LifeFitAI.Application.Identity;
using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Abstractions;

namespace LifeFitAI.Application.Tests.Identity;

internal sealed class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakeActor : ICurrentActor
{
    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }
}

internal sealed class FakeClient : IClientContext
{
    public string? IpAddress { get; set; } = "127.0.0.1";

    public string? UserAgent { get; set; } = "tests";
}

internal sealed class CapturingNotifier : IPasswordResetNotifier
{
    public List<(string Email, string Token)> Sent { get; } = [];

    public Task NotifyAsync(string email, string rawToken, CancellationToken cancellationToken)
    {
        Sent.Add((email, rawToken));
        return Task.CompletedTask;
    }
}

internal sealed class IdentityStore
{
    public FakeClock Clock { get; } = new();

    public FakeActor Actor { get; } = new();

    public FakeClient Client { get; } = new();

    public CapturingNotifier Notifier { get; } = new();

    public bool FailNextAudit { get; set; }

    public List<UserAccount> Users { get; } = [];

    public List<Tenant> Tenants { get; } = [];

    public List<TenantMember> Members { get; } = [];

    public List<Role> Roles { get; } = [];

    public List<Permission> Permissions { get; } = [];

    public List<RolePermission> RolePermissions { get; } = [];

    public List<UserRole> UserRoles { get; } = [];

    public List<Subscription> Subscriptions { get; } = [];

    public List<SubscriptionSeat> Seats { get; } = [];

    public List<RefreshToken> RefreshTokens { get; } = [];

    public List<PasswordResetToken> Resets { get; } = [];

    public List<AuditLog> Audits { get; } = [];

    private bool _inTransaction;

    private readonly List<UserAccount> _pendingUsers = [];
    private readonly List<Tenant> _pendingTenants = [];
    private readonly List<TenantMember> _pendingMembers = [];
    private readonly List<UserRole> _pendingRoles = [];
    private readonly List<Subscription> _pendingSubscriptions = [];
    private readonly List<SubscriptionSeat> _pendingSeats = [];
    private readonly List<RefreshToken> _pendingRefresh = [];
    private readonly List<PasswordResetToken> _pendingResets = [];
    private readonly List<AuditLog> _pendingAudits = [];

    public IEnumerable<UserAccount> VisibleUsers => Users.Concat(_pendingUsers);

    public IEnumerable<Tenant> VisibleTenants => Tenants.Concat(_pendingTenants);

    public IEnumerable<TenantMember> VisibleMembers => Members.Concat(_pendingMembers);

    public IEnumerable<UserRole> VisibleUserRoles => UserRoles.Concat(_pendingRoles);

    public IEnumerable<Subscription> VisibleSubscriptions => Subscriptions.Concat(_pendingSubscriptions);

    public IEnumerable<SubscriptionSeat> VisibleSeats => Seats.Concat(_pendingSeats);

    public IEnumerable<RefreshToken> VisibleRefresh => RefreshTokens.Concat(_pendingRefresh);

    public IEnumerable<PasswordResetToken> VisibleResets => Resets.Concat(_pendingResets);

    public void Stage(UserAccount user) => _pendingUsers.Add(user);

    public void Stage(Tenant tenant) => _pendingTenants.Add(tenant);

    public void Stage(TenantMember member) => _pendingMembers.Add(member);

    public void Stage(UserRole role) => _pendingRoles.Add(role);

    public void Stage(Subscription subscription) => _pendingSubscriptions.Add(subscription);

    public void Stage(SubscriptionSeat seat) => _pendingSeats.Add(seat);

    public void Stage(RefreshToken token) => _pendingRefresh.Add(token);

    public void Stage(PasswordResetToken token) => _pendingResets.Add(token);

    public void Stage(AuditLog audit)
    {
        if (FailNextAudit)
        {
            FailNextAudit = false;
            throw new InvalidOperationException("Audit failed.");
        }

        _pendingAudits.Add(audit);
    }

    public Task<int> SaveChangesAsync()
    {
        if (_inTransaction)
        {
            return Task.FromResult(0);
        }

        CommitPending();
        return Task.FromResult(1);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        _inTransaction = true;
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            CommitPending();
        }
        catch
        {
            ClearPending();
            throw;
        }
        finally
        {
            _inTransaction = false;
        }
    }

    private void CommitPending()
    {
        Users.AddRange(_pendingUsers);
        Tenants.AddRange(_pendingTenants);
        Members.AddRange(_pendingMembers);
        UserRoles.AddRange(_pendingRoles);
        Subscriptions.AddRange(_pendingSubscriptions);
        Seats.AddRange(_pendingSeats);
        RefreshTokens.AddRange(_pendingRefresh);
        Resets.AddRange(_pendingResets);
        Audits.AddRange(_pendingAudits);
        ClearPending();
    }

    private void ClearPending()
    {
        _pendingUsers.Clear();
        _pendingTenants.Clear();
        _pendingMembers.Clear();
        _pendingRoles.Clear();
        _pendingSubscriptions.Clear();
        _pendingSeats.Clear();
        _pendingRefresh.Clear();
        _pendingResets.Clear();
        _pendingAudits.Clear();
    }
}
