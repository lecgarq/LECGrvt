using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISchemaElementScanService
    {
        HashSet<Guid> ScanForThirdPartySchemas(Document doc, Action<string>? logCallback = null);
    }
}
