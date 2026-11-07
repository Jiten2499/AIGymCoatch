using System.Text.RegularExpressions;

namespace LifeFitAI.Application.Storage;

public static partial class StorageKeys
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
        "text/plain",
    };

    public static bool IsSafeLogicalKey(Guid tenantId, string logicalKey)
    {
        if (string.IsNullOrWhiteSpace(logicalKey) || logicalKey.Length > 500)
        {
            return false;
        }

        if (logicalKey.Contains("..", StringComparison.Ordinal) || logicalKey.Contains('\\', StringComparison.Ordinal) || logicalKey.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var prefix = $"tenants/{tenantId:D}/";
        if (!logicalKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LogicalKeyRegex().IsMatch(logicalKey);
    }

    public static string? CategoryFromKey(string logicalKey)
    {
        var parts = logicalKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        return parts[2].ToLowerInvariant() switch
        {
            "members" => parts.Length >= 5 && parts[4].Equals("progress", StringComparison.OrdinalIgnoreCase) ? "progress" : "members",
            "communications" => "communications",
            "gym" => "gym",
            "chat" => "other",
            "other" => "other",
            _ => "other",
        };
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            return "file.bin";
        }

        name = InvalidFileNameChars().Replace(name, "_");
        return name.Length <= 200 ? name : name[..200];
    }

    public static bool IsAllowedContentType(string contentType) => AllowedContentTypes.Contains(contentType);

    public static bool IsAllowedSize(long size) => size > 0 && size <= MaxFileBytes;

    public static string MembersProfileKey(Guid tenantId, Guid memberId, Guid fileId) =>
        $"tenants/{tenantId:D}/members/{memberId:D}/profile/{fileId:D}";

    public static string CommunicationKey(Guid tenantId, Guid communicationId, Guid fileId) =>
        $"tenants/{tenantId:D}/communications/{communicationId:D}/{fileId:D}";

    public static string GymKey(Guid tenantId, Guid fileId) =>
        $"tenants/{tenantId:D}/gym/{fileId:D}";

    public static string OtherKey(Guid tenantId, Guid fileId) =>
        $"tenants/{tenantId:D}/other/{fileId:D}";

    public static string ChatKey(Guid tenantId, Guid conversationId, Guid fileId) =>
        $"tenants/{tenantId:D}/chat/{conversationId:D}/{fileId:D}";

    [GeneratedRegex(
        @"^tenants/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/((members/[0-9a-fA-F-]{36}/profile/[0-9a-fA-F-]{36})|(members/[0-9a-fA-F-]{36}/progress/[0-9a-fA-F-]{36}/[0-9a-fA-F-]{36})|(communications/[0-9a-fA-F-]{36}/[0-9a-fA-F-]{36})|(gym/[0-9a-fA-F-]{36})|(other/[0-9a-fA-F-]{36})|(chat/[0-9a-fA-F-]{36}/[0-9a-fA-F-]{36}))$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LogicalKeyRegex();

    [GeneratedRegex(@"[^\w\.\-\(\) ]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFileNameChars();
}
