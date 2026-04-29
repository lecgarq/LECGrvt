namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionNamingService
    {
        string ResolveTargetFamilyName(Autodesk.Revit.DB.Document doc, string sourceFamilyName, string customName);
    }
}
