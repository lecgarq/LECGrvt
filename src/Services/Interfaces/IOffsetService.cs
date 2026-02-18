using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IOffsetService
    {
        bool TryOffsetElement(Document doc, Element elem, double offset, Action<string>? logCallback = null);
    }
}
