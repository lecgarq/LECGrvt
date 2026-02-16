using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeLineStyleService
    {
        int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null);
    }
}
