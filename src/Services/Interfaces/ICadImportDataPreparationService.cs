using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;

namespace LECG.Services.Interfaces
{
    public interface ICadImportDataPreparationService
    {
        CadData Prepare(Document doc, ImportInstance cadInstance, Action<double, string>? progress = null);
    }
}
