using System;
using Autodesk.Revit.DB;
using LECG.ViewModels;

namespace LECG.Services
{
    public interface ISexyRevitService
    {
        void ApplyBeauty(Document doc, View view, SexyRevitViewModel settings, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
