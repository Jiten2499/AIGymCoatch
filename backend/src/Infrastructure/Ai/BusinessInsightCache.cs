using LifeFitAI.Application.Gym.Dashboard;
using Microsoft.Extensions.Caching.Memory;

namespace LifeFitAI.Infrastructure.Ai;

public sealed class MemoryBusinessInsightCache : IBusinessInsightCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public MemoryBusinessInsightCache(IMemoryCache cache) => _cache = cache;

    public bool TryGet(string key, out AiInsightSection? value) => _cache.TryGetValue(key, out value);

    public void Set(string key, AiInsightSection value) => _cache.Set(key, value, Lifetime);
}
