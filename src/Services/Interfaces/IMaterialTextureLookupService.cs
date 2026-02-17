namespace LECG.Services.Interfaces
{
    public interface IMaterialTextureLookupService
    {
        string? FindTextureFile(string folder, string partialName);
    }
}
