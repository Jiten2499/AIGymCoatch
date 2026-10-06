namespace LifeFitAI.Application.Common.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base("Not Found", message, 404)
    {
    }
}
