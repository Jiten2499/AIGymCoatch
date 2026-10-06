namespace LifeFitAI.Application.Common.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base("Conflict", message, 409)
    {
    }
}
