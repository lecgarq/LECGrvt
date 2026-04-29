using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISexyRevitService
    {
        void ApplyBeauty(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter);
        void ApplyBeauty(Document doc, View view, SexyRevitSettings settings, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
