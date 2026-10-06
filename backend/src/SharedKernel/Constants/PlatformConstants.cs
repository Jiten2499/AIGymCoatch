namespace LifeFitAI.SharedKernel.Constants;

public static class HeaderNames
{
    public const string CorrelationId = "X-Correlation-ID";
}

public static class Correlation
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? Id
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

public static class SchemaNames
{
    public const string Application = "app";
}
