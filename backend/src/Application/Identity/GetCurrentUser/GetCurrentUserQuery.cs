using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Identity.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<UserProfile>>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfile>>
{
    private readonly ICurrentActor _actor;
    private readonly IUserAccountRepository _users;

    public GetCurrentUserQueryHandler(ICurrentActor actor, IUserAccountRepository users)
    {
        _actor = actor;
        _users = users;
    }

    public async Task<Result<UserProfile>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (_actor.UserId is not Guid userId)
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<UserProfile>(IdentityErrors.InvalidCredentials);
        }

        return Result.Success(UserProfiles.From(user));
    }
}
