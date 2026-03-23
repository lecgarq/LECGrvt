using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface INativePurgeDocumentService
    {
        int PurgeUnused(Document doc, IProgressReporter reporter);
    }
}
