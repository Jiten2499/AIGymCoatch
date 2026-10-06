namespace LifeFitAI.Application.Common.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base("Forbidden", message, 403)
    {
    }
}
