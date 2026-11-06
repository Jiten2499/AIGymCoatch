using LifeFitAI.Domain.Chat;
using LifeFitAI.Domain.Gym;
using LifeFitAI.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeFitAI.Infrastructure.Persistence;

public sealed partial class ChatRepository
{
    public async Task<(string Key, string ContentType)?> FindPortraitAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var person = await MapPersonAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        if (person is null || !person.HasPhoto)
        {
            return null;
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId && item.Status == UserStatus.Active, cancellationToken).ConfigureAwait(false);
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        var trainer = await _db.TrainerProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : Portrait(tenantId, user, member, trainer, person.Role);
    }

    private static (string Key, string ContentType)? Portrait(Guid tenantId, UserAccount user, MemberProfile? member, TrainerProfile? trainer, ChatPartyRole role)
    {
        var memberPhoto = Owned(member?.PhotoStorageKey, member?.PhotoContentType, $"tenants/{tenantId:D}/members/{member?.Id:D}/profile/");
        var trainerPhoto = Owned(trainer?.PhotoStorageKey, trainer?.PhotoContentType, $"tenants/{tenantId:D}/trainers/{trainer?.Id:D}/profile/");
        var userPhoto = user.PhotoContentType is not null && UserAccount.OwnsPhoto(user.TenantId, user.Id, user.PhotoStorageKey)
            ? (user.PhotoStorageKey!, user.PhotoContentType)
            : ((string Key, string ContentType)?)null;
        if (role == ChatPartyRole.Member && memberPhoto is not null)
        {
            return memberPhoto;
        }

        if (role == ChatPartyRole.Trainer && trainerPhoto is not null)
        {
            return trainerPhoto;
        }

        return userPhoto ?? memberPhoto ?? trainerPhoto;
    }

    private static (string Key, string ContentType)? Owned(string? key, string? contentType, string prefix) =>
        key is not null && contentType is not null && key.StartsWith(prefix, StringComparison.Ordinal) ? (key, contentType) : null;
}
