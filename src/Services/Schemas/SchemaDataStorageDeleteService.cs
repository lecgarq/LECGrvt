using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SchemaDataStorageDeleteService : ISchemaDataStorageDeleteService
    {
        public int DeleteDataStorageElements(Document doc, IEnumerable<ElementId> ids, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(ids);

            int deleted = 0;
            foreach (ElementId id in ids)
            {
                try
                {
                    doc.Delete(id);
                    deleted++;
                }
                catch
                {
                }
            }

            return deleted;
        }
    }
}
