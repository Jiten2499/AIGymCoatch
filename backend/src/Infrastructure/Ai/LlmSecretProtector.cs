using System.Security.Cryptography;
using LifeFitAI.Application.Ai;
using Microsoft.AspNetCore.DataProtection;

namespace LifeFitAI.Infrastructure.Ai;

public sealed class DataProtectionLlmSecretProtector : ILlmSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionLlmSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("LifeFitAI.LlmConnection.ApiKey");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
