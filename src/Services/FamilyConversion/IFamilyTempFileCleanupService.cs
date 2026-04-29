namespace LECG.Services.Interfaces
{
    public interface IFamilyTempFileCleanupService
    {
        void Cleanup(string tempFamilyPath, bool isTemporary);
    }
}
