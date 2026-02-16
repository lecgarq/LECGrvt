using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeLevelService
    {
        int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null);
    }
}
