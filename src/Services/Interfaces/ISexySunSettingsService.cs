using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISexySunSettingsService
    {
        void Apply(View view, SexyRevitSettings settings, IProgressReporter reporter);
    }
}
