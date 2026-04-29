using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISexySectionBoxVisibilityService
    {
        void Apply(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter);
    }
}
