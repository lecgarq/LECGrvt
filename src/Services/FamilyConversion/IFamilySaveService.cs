using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilySaveService
    {
        string SaveTemp(Document targetFamilyDoc, string targetFamilyName);
    }
}
