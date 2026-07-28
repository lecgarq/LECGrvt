using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class PurgeDeleteElementService
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
            catch (Exception ex) when (IsExpectedDeleteException(ex))
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }

        // Revit's ArgumentException ("ElementId cannot be deleted") derives from
        // Autodesk.Revit.Exceptions.ApplicationException, NOT System.ArgumentException,
        // so it must be listed explicitly or it escapes and aborts the whole purge pass.
        private static bool IsExpectedDeleteException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
