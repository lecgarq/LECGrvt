using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeParameterService
    {
        int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null);
    }
}
