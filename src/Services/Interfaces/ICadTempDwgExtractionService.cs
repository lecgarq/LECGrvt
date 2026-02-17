using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadTempDwgExtractionService
    {
        CadData Extract(Document doc, string templatePath, string dwgPath, Action<double, string>? progress = null);
    }
}
