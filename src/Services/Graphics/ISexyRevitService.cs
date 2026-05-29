using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISexyRevitService
    {
        void ApplyBeauty(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter);
    }
}
