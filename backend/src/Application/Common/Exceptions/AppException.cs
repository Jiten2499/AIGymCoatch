namespace LifeFitAI.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string title, string message, int statusCode)
        : base(message)
    {
        Title = title;
        StatusCode = statusCode;
    }

    public string Title { get; }

    public int StatusCode { get; }
}
