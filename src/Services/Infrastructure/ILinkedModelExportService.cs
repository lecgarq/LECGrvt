using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ILinkedModelExportService
    {
        Dictionary<string, List<ElementId>> GroupElementsByType(Document doc);

        void ExportAndLink(Document doc, Dictionary<string, List<ElementId>> selectedGroups, string outputFolder, IProgressReporter reporter);
    }
}
