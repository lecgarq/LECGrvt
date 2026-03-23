using System;

namespace LECG.Services.Interfaces
{
    public interface IAppMemoryCache
    {
        bool TryGetValue<T>(string key, out T? value);
        T GetOrCreate<T>(string key, Func<T> factory, TimeSpan absoluteExpirationRelativeToNow, TimeSpan? slidingExpiration = null);
        void Remove(string key);
    }
}
