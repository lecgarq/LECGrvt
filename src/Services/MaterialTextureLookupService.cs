using System;
using System.Linq;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialTextureLookupService : IMaterialTextureLookupService
    {
        public string? FindTextureFile(string folder, string partialName)
        {
            if (!System.IO.Directory.Exists(folder)) return null;

            return System.IO.Directory.GetFiles(folder)
                .FirstOrDefault(f => System.IO.Path.GetFileName(f).IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
