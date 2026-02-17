using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadSourceCleanupService : ICadSourceCleanupService
    {
        public void DeleteOriginalIfPresent(Document doc, ElementId deleteId)
        {
            if (deleteId == null || deleteId == ElementId.InvalidElementId)
            {
                return;
            }

            Element e = doc.GetElement(deleteId);
            if (e == null)
            {
                return;
            }

            if (e.Pinned)
            {
                e.Pinned = false;
            }

            doc.Delete(deleteId);
        }
    }
}
