using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISchemaEraseService
    {
        int EraseSchemas(Document doc, IEnumerable<Guid> guids, Action<string>? logCallback = null);
    }
}
