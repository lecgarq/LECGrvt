using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class BatchRenameExecutionService : IBatchRenameExecutionService
    {
        private readonly ITransactionService _transactionService;

        public BatchRenameExecutionService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            return ExecuteBatchRename(doc, items, logger, new LegacyProgressReporter(onProgress, logger.Log));
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Logging.ILogger logger, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(reporter);

            int count = 0;
            int total = items.Count;
            int current = 0;

            List<ReplaceItem> standardItems = new List<ReplaceItem>();
            List<ReplaceItem> familyItems = new List<ReplaceItem>();

            foreach (var item in items)
            {
                if (item.Type == "FamilyParameter")
                    familyItems.Add(item);
                else
                    standardItems.Add(item);
            }

            logger.Log($"Starting batch rename for {total} items ({standardItems.Count} standard, {familyItems.Count} family parameters)...");

            // 1. Process Standard Items (Transaction Required)
            if (standardItems.Count > 0)
            {
                _transactionService.Run(doc, "Batch Rename", currentDoc =>
                {
                    foreach (var item in standardItems)
                    {
                        current++;
                        double percent = (double)current / total * 100;

                        if (!item.IsChecked) continue;

                        ElementId id = new ElementId(item.ElementId);
                        Element el = currentDoc.GetElement(id);

                        if (el != null)
                        {
                            try
                            {
                                reporter.Report($"Processing {item.ElementName}...", percent);

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
                                            bool swapped = SwapStyle(currentDoc, gs, item.NewValue, logger);
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
                });
            }

            // 2. Process Family Parameters (No Main Transaction - Uses EditFamily)
            // CRITICAL: Group by Family ID so we call EditFamily+LoadFamily ONCE per family.
            // Calling LoadFamily on a family invalidates the Family element reference — 
            // if we process items one-by-one, subsequent items for the SAME family will
            // crash with "referenced object is not valid" because the handle is stale.
            if (familyItems.Count > 0)
            {
                // Group all checked rename items by their family element ID
                var byFamily = new Dictionary<long, List<ReplaceItem>>();
                foreach (var item in familyItems)
                {
                    if (!item.IsChecked) continue;
                    if (!byFamily.ContainsKey(item.ElementId))
                        byFamily[item.ElementId] = new List<ReplaceItem>();
                    byFamily[item.ElementId].Add(item);
                }

                int familyIndex = 0;
                foreach (var kvp in byFamily)
                {
                    familyIndex++;
                    double percent = (double)familyIndex / byFamily.Count * 100;

                    ElementId familyId = new ElementId(kvp.Key);
                    Element el = doc.GetElement(familyId);

                    if (el is not Family family)
                    {
                        foreach (var item in kvp.Value)
                            logger.LogError($"Skipped: Element {kvp.Key} is not a Family (type={el?.GetType().Name ?? "null"}).");
                        continue;
                    }

                    reporter.Report($"Processing Family '{family.Name}'...", percent);

                    try
                    {
                        Document? famDoc = doc.EditFamily(family);
                        if (famDoc == null)
                        {
                            logger.LogError($"Could not open family document for '{family.Name}'.");
                            continue;
                        }

                        int renamedInFamily = 0;
                        bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
                        {
                            FamilyManager mgr = famDoc.FamilyManager;

                            foreach (var item in kvp.Value)
                            {
                                // Find the parameter by its current (original) name in the family doc
                                FamilyParameter? paramToRename = null;
                                foreach (FamilyParameter fp in mgr.Parameters)
                                {
                                    if (fp.Definition.Name.Equals(item.OriginalValue, StringComparison.Ordinal))
                                    {
                                        paramToRename = fp;
                                        break;
                                    }
                                }

                                if (paramToRename != null)
                                {
                                    try
                                    {
                                        mgr.RenameParameter(paramToRename, item.NewValue);
                                        renamedInFamily++;
                                        count++;
                                        logger.LogSuccess($"Renamed param '{item.OriginalValue}' → '{item.NewValue}' in '{family.Name}'");
                                    }
                                    catch (Exception renameEx)
                                    {
                                        logger.LogError($"Could not rename param '{item.OriginalValue}' in '{family.Name}': {renameEx.Message}");
                                    }
                                }
                                else
                                {
                                    logger.Log($"Skipped: Param '{item.OriginalValue}' not found in family '{family.Name}'.");
                                }
                            }
                            return renamedInFamily > 0;
                        });

                        // Reload ONCE after all parameters are renamed in this family
                        if (committed)
                            famDoc.LoadFamily(doc, new OverwriteFamilyOption());

                        famDoc.Close(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed processing family '{family.Name}': {ex.Message}");
                    }
                }
            }

            logger.LogSuccess($"Batch rename complete. Modified {count} elements.");
            reporter.Report("Done", 100);

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
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Projection); if(w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Projection); } catch (Exception ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set projection line weight: {ex.Message}"); }
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Cut); if(w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Cut); } catch (Exception ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set cut line weight: {ex.Message}"); }
                
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

    public class OverwriteFamilyOption : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(bool sharedFamilyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
