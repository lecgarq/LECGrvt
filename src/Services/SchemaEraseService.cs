using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SchemaEraseService : ISchemaEraseService
    {
        public int EraseSchemas(Document doc, IEnumerable<Guid> guids, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(guids);

            int erased = 0;
            foreach (Guid guid in guids)
            {
                Schema? schema = Schema.Lookup(guid);
                if (schema != null)
                {
                    try
                    {
                        doc.EraseSchemaAndAllEntities(schema);
                        erased++;
                        logCallback?.Invoke($"  Erased: {schema.SchemaName}");
                    }
                    catch
                    {
                    }
                }
            }

            return erased;
        }
    }
}
