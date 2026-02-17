using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ISchemaDataStorageDeleteService
    {
        int DeleteDataStorageElements(Document doc, IEnumerable<ElementId> ids, Action<string>? logCallback = null);
    }
}
