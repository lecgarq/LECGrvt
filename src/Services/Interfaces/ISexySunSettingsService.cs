using System;
using Autodesk.Revit.DB;
using LECG.ViewModels;

namespace LECG.Services.Interfaces
{
    public interface ISexySunSettingsService
    {
        void Apply(View view, SexyRevitViewModel settings, Action<string> log, Action<double, string> progress);
    }
}
