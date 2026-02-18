using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.Core
{
    /// <summary>
    /// Availability class to enable commands ONLY in Project Documents (disabling them in Family Editor).
    /// </summary>
    public class ProjectDocumentAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            ArgumentNullException.ThrowIfNull(applicationData);

            if (applicationData.ActiveUIDocument == null || applicationData.ActiveUIDocument.Document == null)
                return false;

            return !applicationData.ActiveUIDocument.Document.IsFamilyDocument;
        }
    }
}
