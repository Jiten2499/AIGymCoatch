using System.Security.Cryptography;

namespace LifeFitAI.Application.Platform;

internal static class TemporaryPassword
{
    private const string Lowers = "abcdefghijkmnopqrstuvwxyz";
    private const string Uppers = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Specials = "!@#$%";

    public static string Create()
    {
        var alphabet = Lowers + Uppers + Digits + Specials;
        var chars = new char[16];
        chars[0] = Lowers[RandomNumberGenerator.GetInt32(Lowers.Length)];
        chars[1] = Uppers[RandomNumberGenerator.GetInt32(Uppers.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Specials[RandomNumberGenerator.GetInt32(Specials.Length)];
        for (var index = 4; index < chars.Length; index++)
        {
            chars[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        for (var index = chars.Length - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (chars[index], chars[swap]) = (chars[swap], chars[index]);
        }

        return new string(chars);
    }
}
