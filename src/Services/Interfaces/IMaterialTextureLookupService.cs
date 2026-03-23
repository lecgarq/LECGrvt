using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IMaterialTextureLookupService
    {
        string? FindTextureFile(string folder, string partialName);
        Dictionary<string, string?> ScanFolder(string folder);
    }
}
