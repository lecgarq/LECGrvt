using System;
using System.Collections.Generic;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public sealed class AppMemoryCache : IAppMemoryCache
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, CacheEntry> _entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);

        public bool TryGetValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            lock (_sync)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (!TryGetValueCore(key, now, out object? cachedValue))
                {
                    value = default;
                    return false;
                }

                if (cachedValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                value = default;
                return false;
            }
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
            DateTimeOffset now = DateTimeOffset.UtcNow;

            lock (_sync)
            {
                if (TryGetValueCore(key, now, out object? existingValue) && existingValue is T existingTypedValue)
                {
                    return existingTypedValue;
                }

                _entries[key] = new CacheEntry(
                    createdValue!,
                    now + absoluteExpirationRelativeToNow,
                    slidingExpiration,
                    now);

                return createdValue;
            }
        }

        public void Remove(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            lock (_sync)
            {
                _entries.Remove(key);
            }
        }

        private bool TryGetValueCore(string key, DateTimeOffset now, out object? value)
        {
            if (!_entries.TryGetValue(key, out CacheEntry? entry))
            {
                value = null;
                return false;
            }

            if (entry.IsExpired(now))
            {
                _entries.Remove(key);
                value = null;
                return false;
            }

            entry.Touch(now);
            value = entry.Value;
            return true;
        }

        private sealed class CacheEntry
        {
            private readonly TimeSpan? _slidingExpiration;
            private DateTimeOffset _lastAccessUtc;

            public CacheEntry(object value, DateTimeOffset absoluteExpirationUtc, TimeSpan? slidingExpiration, DateTimeOffset createdUtc)
            {
                Value = value;
                AbsoluteExpirationUtc = absoluteExpirationUtc;
                _slidingExpiration = slidingExpiration;
                _lastAccessUtc = createdUtc;
            }

            public object Value { get; }

            public DateTimeOffset AbsoluteExpirationUtc { get; }

            public bool IsExpired(DateTimeOffset now)
            {
                if (now >= AbsoluteExpirationUtc)
                {
                    return true;
                }

                return _slidingExpiration.HasValue && now - _lastAccessUtc >= _slidingExpiration.Value;
            }

            public void Touch(DateTimeOffset now)
            {
                if (_slidingExpiration.HasValue)
                {
                    _lastAccessUtc = now;
                }
            }
        }
    }
}
