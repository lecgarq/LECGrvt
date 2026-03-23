using LECG.Services;
using LECG.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using FluentAssertions;

namespace LECG.Tests.Caching;

public class AppMemoryCacheTests
{
    [Fact]
    public void GetOrCreate_reuses_cached_value_until_removed()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        IAppMemoryCache cache = new AppMemoryCache(memoryCache);
        int factoryCalls = 0;

        string first = cache.GetOrCreate("cache-key", () =>
        {
            factoryCalls++;
            return "value";
        }, TimeSpan.FromMinutes(1));

        string second = cache.GetOrCreate("cache-key", () =>
        {
            factoryCalls++;
            return "other";
        }, TimeSpan.FromMinutes(1));

        cache.Remove("cache-key");

        string third = cache.GetOrCreate("cache-key", () =>
        {
            factoryCalls++;
            return "fresh";
        }, TimeSpan.FromMinutes(1));

        first.Should().Be("value");
        second.Should().Be("value");
        third.Should().Be("fresh");
        factoryCalls.Should().Be(2);
    }
}
