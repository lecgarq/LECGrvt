using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilySaveLoadService
    {
        string SaveAndLoad(Document projectDoc, Document familyDoc, string targetFamilyName);
    }
}
