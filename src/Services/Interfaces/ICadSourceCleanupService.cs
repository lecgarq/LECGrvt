using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadSourceCleanupService
    {
        void DeleteOriginalIfPresent(Document doc, ElementId deleteId);
    }
}
