using System.Security.Cryptography;
using System.Text;
using LifeFitAI.Application.Identity;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly PasswordOptions _options;

    public Pbkdf2PasswordHasher(IOptions<PasswordOptions> options)
    {
        _options = options.Value;
    }

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _options.Iterations,
            HashAlgorithmName.SHA256,
            _options.HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(salt),
            _options.Iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class PasswordPolicy : IPasswordPolicy
{
    private readonly PasswordOptions _options;

    public PasswordPolicy(IOptions<PasswordOptions> options)
    {
        _options = options.Value;
        Description = $"Password must be at least {_options.MinimumLength} characters and include uppercase, lowercase, a number, and a special character.";
    }

    public string Description { get; }

    public bool IsValid(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < _options.MinimumLength)
        {
            return false;
        }

        if (_options.RequireUppercase && !password.Any(char.IsUpper))
        {
            return false;
        }

        if (_options.RequireLowercase && !password.Any(char.IsLower))
        {
            return false;
        }

        if (_options.RequireDigit && !password.Any(char.IsDigit))
        {
            return false;
        }

        if (_options.RequireSpecial && password.All(char.IsLetterOrDigit))
        {
            return false;
        }

        return true;
    }
}

public sealed class Sha256SecretTokenFactory : ISecretTokenFactory
{
    public string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
