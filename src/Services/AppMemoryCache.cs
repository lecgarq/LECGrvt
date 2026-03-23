using System;
using LECG.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace LECG.Services
{
    public sealed class AppMemoryCache : IAppMemoryCache
    {
        private readonly IMemoryCache _memoryCache;

        public AppMemoryCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (_memoryCache.TryGetValue(key, out object? cachedValue) && cachedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan absoluteExpirationRelativeToNow, TimeSpan? slidingExpiration = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(factory);

            if (TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                return cachedValue;
            }

            T createdValue = factory();
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow,
                SlidingExpiration = slidingExpiration
            };

            _memoryCache.Set(key, createdValue, options);
            return createdValue;
        }

        public void Remove(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _memoryCache.Remove(key);
        }
    }
}
