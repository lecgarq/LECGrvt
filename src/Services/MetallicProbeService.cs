using System;
using System.Collections.Concurrent;
using LECG.Core.Substance;
using LECG.Services.Imaging;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MetallicProbeService : IMetallicProbeService
    {
        private const int ProbeSize = 64;
        private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

        public bool IsMetallic(string metallicPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metallicPath);
            return _cache.GetOrAdd(metallicPath, p =>
            {
                try
                {
                    var (gray, _, _) = PngIo.LoadGray8(p, ProbeSize);
                    return PbrBakeMath.IsMetallic(PbrBakeMath.MaxGray(gray));
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is NotSupportedException || ex is ArgumentException)
                {
                    return false;
                }
            });
        }
    }
}
