using System.Security.Cryptography;
using LifeFitAI.Application.Storage;
using Microsoft.AspNetCore.DataProtection;

namespace LifeFitAI.Infrastructure.Storage;

public sealed class DataProtectionStorageSecretProtector : IStorageSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionStorageSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("LifeFitAI.GoogleDrive.RefreshToken");

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
