using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeDeleteElementService
    {
        bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback);
    }
}
