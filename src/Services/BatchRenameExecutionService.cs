using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class BatchRenameExecutionService : IBatchRenameExecutionService
    {
        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            int count = 0;
            int total = items.Count;
            int current = 0;

            logger.Log($"Starting batch rename for {total} items...");

            using (Transaction t = new Transaction(doc, "Batch Rename"))
            {
                t.Start();

                foreach (var item in items)
                {
                    current++;
                    double percent = (double)current / total * 100;

                    if (!item.IsChecked) continue;

                    ElementId id = new ElementId(item.ElementId);
                    Element el = doc.GetElement(id);

                    if (el != null)
                    {
                        try
                        {
                            onProgress?.Invoke(percent, $"Processing {item.ElementName}...");

                            if (string.Equals(el.Name, item.NewValue, StringComparison.Ordinal)) continue;

                            el.Name = item.NewValue;
                            count++;
                            logger.LogSuccess($"Renamed '{item.OriginalValue}' to '{item.NewValue}'");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"ERROR renaming {item.ElementName}: {ex.Message}");
                        }
                    }
                }

                t.Commit();
            }

            logger.LogSuccess($"Batch rename complete. Modified {count} elements.");
            onProgress?.Invoke(100, "Done");

            return count;
        }
    }
}
