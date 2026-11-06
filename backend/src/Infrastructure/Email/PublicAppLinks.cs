using LifeFitAI.Application.Email;
using Microsoft.Extensions.Options;

namespace LifeFitAI.Infrastructure.Email;

public sealed class PublicAppLinks : IPublicAppLinks
{
    private readonly string _baseUrl;

    public PublicAppLinks(IOptions<PublicAppEmailOptions> options) =>
        _baseUrl = (options.Value.PublicAppBaseUrl ?? "http://localhost:3000").TrimEnd('/');

    public string LoginUrl => $"{_baseUrl}/login";

    public string ActivationUrl(string token) => $"{_baseUrl}/activate?token={Uri.EscapeDataString(token)}";

    public string MobileActivationUrl(string token) => $"lifefit://activate?token={Uri.EscapeDataString(token)}";
}
