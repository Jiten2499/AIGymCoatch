using LifeFitAI.Application.Identity;
using Microsoft.AspNetCore.Http;

namespace LifeFitAI.Api.Security;

public sealed class HttpCurrentActor : ICurrentActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentActor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => Read("sub");

    public Guid? TenantId => Read("tenant_id");

    private Guid? Read(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public sealed class HttpClientContext : IClientContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpClientContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
