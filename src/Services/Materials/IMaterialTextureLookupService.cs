using System.Collections.Generic;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IMaterialTextureLookupService
    {
        string? FindTextureFile(string folder, string partialName);
        string DeriveMaterialName(string diffusePath);
        Dictionary<string, string?> ScanFolder(string folder);
        IReadOnlyList<PbrMaterialFolderCandidate> ScanImmediateMaterialFolders(string rootFolder);
    }
}
