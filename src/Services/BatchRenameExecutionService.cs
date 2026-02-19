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
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);

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

                            // Special handling for GraphicsStyle (Object Styles / Line Styles)
                            if (el is GraphicsStyle gs)
                            {
                                try 
                                {
                                    // Try updating the element name directly
                                    // This often fails for certain built-in or imported styles
                                    gs.Name = item.NewValue; 
                                    count++;
                                }
                                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                                {
                                    // Known Revit API limitation: cannot rename some subcategories directly
                                    // Fallback: Check if it's a subcategory and if we can utilize a workaround (simplified text for user)
                                    logger.LogError($"Skipped '{item.OriginalValue}': Renaming this specific Object Style is restricted by the Revit API.");
                                    continue;
                                }
                                catch (Exception innerEx)
                                {
                                     logger.LogError($"Failed to rename style '{item.OriginalValue}': {innerEx.Message}");
                                     continue;
                                }
                            }
                            else
                            {
                                el.Name = item.NewValue;
                                count++;
                            }

                            logger.LogSuccess($"Renamed '{item.OriginalValue}' to '{item.NewValue}'");
                        }
                        catch (Exception ex)
                        {
                            // Catch-all for other element types
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
