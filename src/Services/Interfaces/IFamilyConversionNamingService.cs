namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionNamingService
    {
        string ResolveTargetFamilyName(string sourceFamilyName, string customName);
    }
}
