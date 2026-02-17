using System;
using Autodesk.Revit.DB;
using LECG.ViewModels;

namespace LECG.Services.Interfaces
{
    public interface ISexySectionBoxVisibilityService
    {
        void Apply(Document doc, View view, SexyRevitViewModel settings, Action<string> log, Action<double, string> progress);
    }
}
