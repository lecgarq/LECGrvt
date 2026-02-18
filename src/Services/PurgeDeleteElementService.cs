using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeDeleteElementService : IPurgeDeleteElementService
    {
        public bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(name);

            try
            {
                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }
    }
}
