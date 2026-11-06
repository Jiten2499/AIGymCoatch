using LifeFitAI.Domain.Identity;
using LifeFitAI.SharedKernel.Results;

namespace LifeFitAI.Application.Chat;

public sealed partial class ChatMessageService
{
    public async Task<Result<ChatPhotoFile>> PhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var scope = await GateAsync(PermissionCodes.ChatRead, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return Result.Failure<ChatPhotoFile>(scope.Error);
        }

        var portrait = await _chats.FindPortraitAsync(scope.Value.TenantId, userId, cancellationToken).ConfigureAwait(false);
        if (portrait is null || !_portraits.IsSafeKey(portrait.Value.Key))
        {
            return Result.Failure<ChatPhotoFile>(ChatErrors.NotFound);
        }

        var content = await _portraits.ReadAsync(portrait.Value.Key, cancellationToken).ConfigureAwait(false);
        return content is null
            ? Result.Failure<ChatPhotoFile>(ChatErrors.NotFound)
            : Result.Success(new ChatPhotoFile(content, portrait.Value.ContentType));
    }
}
