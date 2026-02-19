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
                                    // Fallback: Attempt destructive "Swap & Delete" strategy
                                    if (gs.GraphicsStyleCategory != null)
                                    {
                                        bool swapped = SwapStyle(doc, gs, item.NewValue, logger);
                                        if (swapped)
                                        {
                                            count++;
                                            logger.LogSuccess($"Renamed (via Swap) '{item.OriginalValue}' to '{item.NewValue}'");
                                        }
                                        else
                                        {
                                            logger.LogError($"Skipped '{item.OriginalValue}': API restricted & Swap failed.");
                                        }
                                    }
                                    else
                                    {
                                         logger.LogError($"Skipped '{item.OriginalValue}': Renaming this specific Object Style is restricted by the Revit API.");
                                    }
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

        private bool SwapStyle(Document doc, GraphicsStyle oldStyle, string newName, Logging.ILogger logger)
        {
            try
            {
                Category oldCat = oldStyle.GraphicsStyleCategory;
                if (oldCat == null || oldCat.Parent == null) return false;

                // 1. Create New Subcategory
                Category parentCat = oldCat.Parent;
                Category newCat;
                try
                {
                    newCat = doc.Settings.Categories.NewSubcategory(parentCat, newName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Name might already exist, try to find it
                    if (parentCat.SubCategories.Contains(newName))
                        newCat = parentCat.SubCategories.get_Item(newName);
                    else
                        return false;
                }

                // 2. Copy Properties
                newCat.LineColor = oldCat.LineColor;
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Projection); if(w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Projection); } catch { }
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Cut); if(w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Cut); } catch { }
                
                // 3. Find Elements using the OLD style (CurveElements mostly)
                // Note: This is simplified and mainly targets Line Styles (Model/Detail Lines)
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(CurveElement));
                
                int movedCount = 0;
                foreach (Element e in collector)
                {
                    if (e is CurveElement curve)
                    {
                         // CurveElement uses LineStyle property which is the GraphicsStyle element
                         if (curve.LineStyle.Id == oldStyle.Id)
                         {
                             // Find the GraphicsStyle element corresponding to the NEW Category
                             // We need to find the correct GraphicsStyle (Projection usually for lines)
                             GraphicsStyle? newGs = newCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                             if (newGs != null)
                             {
                                 curve.LineStyle = newGs;
                                 movedCount++;
                             }
                         }
                    }
                }

                // 4. Try Delete Old (Might fail if used elsewhere)
                try
                {
                    doc.Delete(oldStyle.GraphicsStyleCategory.Id);
                }
                catch
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Swap failed for {oldStyle.Name}: {ex.Message}");
                return false;
            }
        }
    }
}
