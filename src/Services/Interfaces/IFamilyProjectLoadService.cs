using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyProjectLoadService
    {
        void Load(Document doc, string tempFamilyPath);
    }
}
