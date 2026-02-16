using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.Core
{
    /// <summary>
    /// Availability class to enable commands ONLY in Family Documents.
    /// </summary>
    public class FamilyDocumentAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            if (applicationData.ActiveUIDocument == null || applicationData.ActiveUIDocument.Document == null)
                return false;

            return applicationData.ActiveUIDocument.Document.IsFamilyDocument;
        }
    }
}
