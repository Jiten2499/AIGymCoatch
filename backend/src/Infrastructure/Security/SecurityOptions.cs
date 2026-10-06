namespace LifeFitAI.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "LifeFitAI";

    public string Audience { get; set; } = "LifeFitAI";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    public int PasswordResetMinutes { get; set; } = 30;
}

public sealed class PasswordOptions
{
    public const string SectionName = "Password";

    public int Iterations { get; set; } = 210_000;

    public int SaltSize { get; set; } = 16;

    public int HashSize { get; set; } = 32;

    public int MinimumLength { get; set; } = 12;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireDigit { get; set; } = true;

    public bool RequireSpecial { get; set; } = true;
}
