using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilySaveService
    {
        string Save(Document familyDoc, string name);
    }
}
