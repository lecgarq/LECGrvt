using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

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
                // Never attempt deletion of invalid/system ids.
                if (id.Value <= 0)
                {
                    logCallback?.Invoke($"  Skipped system/invalid id for '{name}'.");
                    return false;
                }

                Element? element = doc.GetElement(id);
                if (element == null || !element.IsValidObject)
                {
                    logCallback?.Invoke($"  Skipped '{name}' (element not found or invalid).");
                    return false;
                }

                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (ArgumentException ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }
    }
}
