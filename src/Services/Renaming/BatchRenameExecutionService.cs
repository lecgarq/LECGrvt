using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core.Rename;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class BatchRenameExecutionService : IBatchRenameExecutionService
    {
        private readonly ITransactionService _transactionService;
        private readonly IFamilyLoadOptionsFactory _loadOptionsFactory;

        public BatchRenameExecutionService(ITransactionService transactionService, IFamilyLoadOptionsFactory loadOptionsFactory)
        {
            _transactionService = transactionService;
            _loadOptionsFactory = loadOptionsFactory;
        }

        public int ExecuteBatchRename(Document doc, List<ElementRowViewModel> items, Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);

            return ExecuteBatchRename(doc, items, logger, new LegacyProgressReporter(onProgress, logger.Log));
        }

        public int ExecuteBatchRename(Document doc, List<ElementRowViewModel> items, Logging.ILogger logger, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(reporter);

            int count = 0;
            int total = items.Count;
            int current = 0;

            List<ElementRowViewModel> standardItems = new List<ElementRowViewModel>();
            List<ElementRowViewModel> familyItems = new List<ElementRowViewModel>();

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

                        ElementId id = new ElementId(item.Id);
                        Element el = currentDoc.GetElement(id);

                        if (el != null)
                        {
                            try
                            {
                                reporter.Report($"Processing {item.Name}...", percent);

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
                                    catch (ArgumentException innerEx)
                                    {
                                        logger.LogError($"Failed to rename style '{item.OriginalValue}': {innerEx.Message}");
                                        continue;
                                    }
                                    catch (InvalidOperationException innerEx)
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
                            catch (Exception ex) when (IsExpectedRenameException(ex))
                            {
                                logger.LogError($"ERROR renaming {item.Name}: {ex.Message}");
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
                Dictionary<long, List<ElementRowViewModel>> byFamily = GroupCheckedFamilyParameterItems(familyItems);

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

                    string familyName = family.Name;
                    reporter.Report($"Processing Family '{familyName}'...", percent);

                    Document? famDoc = null;
                    try
                    {
                        famDoc = doc.EditFamily(family);
                        if (famDoc == null)
                        {
                            logger.LogError($"Could not open family document for '{familyName}'.");
                            continue;
                        }

                        // Pre-validate: build safety sets to detect parameters that could break the family
                        FamilyManager preCheckMgr = famDoc.FamilyManager;
                        var dimensionLabels = BuildDimensionLabelNames(famDoc);
                        var formulaReferenced = BuildFormulaReferencedNames(preCheckMgr);
                        var elementAssociated = BuildElementAssociationNames(famDoc, preCheckMgr);

                        int renamedInFamily = 0;
                        bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
                        {
                            FamilyManager mgr = famDoc.FamilyManager;

                            renamedInFamily = RenameFamilyParameters(
                                mgr,
                                kvp.Value,
                                familyName,
                                dimensionLabels,
                                formulaReferenced,
                                elementAssociated,
                                logger);
                            count += renamedInFamily;
                            return renamedInFamily > 0;
                        });

                        // Reload ONCE after all parameters are renamed in this family
                        if (committed)
                            famDoc.LoadFamily(doc, _loadOptionsFactory.Create());

                        famDoc.Close(false);
                    }
                    catch (Exception ex) when (IsExpectedRenameException(ex))
                    {
                        logger.LogError($"Failed processing family '{familyName}': {ex.Message}");
                    }
                    finally
                    {
                        TryCloseFamilyDocument(famDoc, familyName, logger);
                    }
                }
            }

            logger.LogSuccess($"Batch rename complete. Modified {count} elements.");
            reporter.Report("Done", 100);

            return count;
        }

        private static Dictionary<long, List<ElementRowViewModel>> GroupCheckedFamilyParameterItems(List<ElementRowViewModel> familyItems)
        {
            Dictionary<long, List<ElementRowViewModel>> byFamily = new Dictionary<long, List<ElementRowViewModel>>();
            foreach (ElementRowViewModel item in familyItems)
            {
                if (!item.IsChecked)
                {
                    continue;
                }

                if (!byFamily.TryGetValue(item.Id, out List<ElementRowViewModel>? items))
                {
                    items = new List<ElementRowViewModel>();
                    byFamily[item.Id] = items;
                }

                items.Add(item);
            }

            return byFamily;
        }

        private static int RenameFamilyParameters(
            FamilyManager manager,
            List<ElementRowViewModel> items,
            string familyName,
            HashSet<string> dimensionLabels,
            HashSet<string> formulaReferenced,
            HashSet<string> elementAssociated,
            Logging.ILogger logger)
        {
            int renamedCount = 0;
            foreach (ElementRowViewModel item in items)
            {
                FamilyParameter? paramToRename = FindFamilyParameterByName(manager, item.OriginalValue);
                if (paramToRename == null)
                {
                    logger.Log($"Skipped: Param '{item.OriginalValue}' not found in family '{familyName}'.");
                    continue;
                }

                string? skipReason = GetRenameSkipReason(
                    paramToRename,
                    item.NewValue,
                    manager,
                    dimensionLabels,
                    formulaReferenced,
                    elementAssociated);
                if (skipReason != null)
                {
                    logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': {skipReason}");
                    continue;
                }

                if (TryRenameFamilyParameter(manager, paramToRename, item, familyName, logger))
                {
                    renamedCount++;
                }
            }

            return renamedCount;
        }

        private static FamilyParameter? FindFamilyParameterByName(FamilyManager manager, string parameterName)
        {
            foreach (FamilyParameter parameter in manager.Parameters)
            {
                if (parameter.Definition.Name.Equals(parameterName, StringComparison.Ordinal))
                {
                    return parameter;
                }
            }

            return null;
        }

        private static bool TryRenameFamilyParameter(
            FamilyManager manager,
            FamilyParameter parameter,
            ElementRowViewModel item,
            string familyName,
            Logging.ILogger logger)
        {
            try
            {
                manager.RenameParameter(parameter, item.NewValue);
                logger.LogSuccess($"Renamed param '{item.OriginalValue}' -> '{item.NewValue}' in '{familyName}'");
                return true;
            }
            catch (ArgumentException renameEx)
            {
                logger.LogError($"Could not rename param '{item.OriginalValue}' in '{familyName}': {renameEx.Message}");
                return false;
            }
            catch (InvalidOperationException renameEx)
            {
                logger.LogError($"Could not rename param '{item.OriginalValue}' in '{familyName}': {renameEx.Message}");
                return false;
            }
            catch (RevitExceptions.InvalidOperationException renameEx)
            {
                logger.LogError($"Could not rename param '{item.OriginalValue}' in '{familyName}': {renameEx.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns null if the parameter is safe to rename, or a reason string explaining why it should be skipped.
        /// Prevents renaming parameters that drive geometry, formulas, dimensions, or element associations.
        /// </summary>
        private static string? GetRenameSkipReason(
            FamilyParameter fp,
            string newName,
            FamilyManager mgr,
            HashSet<string> dimensionLabels,
            HashSet<string> formulaReferenced,
            HashSet<string> elementAssociated)
        {
            string name = fp.Definition.Name;

            // Built-in parameters cannot be renamed
            if (fp.Id.Value < 0)
                return "built-in parameter (cannot rename)";

            // Reporting parameters are dimension-driven — renaming could break references
            if (fp.IsReporting)
                return "reporting parameter (dimension-driven)";

            // Dimension label — renaming could break dimension display
            if (dimensionLabels.Contains(name))
                return "drives a dimension label";

            // Referenced in another parameter's formula — Revit may not auto-update all references
            if (formulaReferenced.Contains(name))
                return "referenced in another parameter's formula";

            // Associated with element properties (geometry, material, visibility, nesting)
            if (elementAssociated.Contains(name))
                return "associated with element geometry/material/visibility";

            // Check if the new name conflicts with an existing parameter name
            foreach (FamilyParameter existing in mgr.Parameters)
            {
                if (existing.Id != fp.Id &&
                    existing.Definition.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"new name '{newName}' conflicts with existing parameter";
                }
            }

            return null; // Safe to rename
        }

        /// <summary>
        /// Build set of parameter names that are used as dimension labels in the family.
        /// </summary>
        private static HashSet<string> BuildDimensionLabelNames(Document famDoc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dimensions = new FilteredElementCollector(famDoc)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>();

            foreach (Dimension dim in dimensions)
            {
                try
                {
                    FamilyParameter? label = dim.FamilyLabel;
                    if (label != null)
                        names.Add(label.Definition.Name);
                }
                catch
                {
                    // Some dimensions may not support FamilyLabel
                }
            }

            return names;
        }

        /// <summary>
        /// Build set of parameter names that are referenced in any other parameter's formula.
        /// </summary>
        private static HashSet<string> BuildFormulaReferencedNames(FamilyManager fm)
        {
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect all parameter names
            var allNames = new List<string>();
            foreach (FamilyParameter fp in fm.Parameters)
                allNames.Add(fp.Definition.Name);

            // Scan formulas for references
            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (string.IsNullOrEmpty(fp.Formula)) continue;

                string formula = fp.Formula;
                foreach (string name in allNames)
                {
                    if (FormulaNameUpdater.ContainsReference(formula, name))
                    {
                        referenced.Add(name);
                    }
                }
            }

            return referenced;
        }

        /// <summary>
        /// Build set of parameter names that are associated with element properties
        /// (geometry, material assignments, visibility, nested family mappings).
        /// </summary>
        private static HashSet<string> BuildElementAssociationNames(Document famDoc, FamilyManager fm)
        {
            var associated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var allElements = new FilteredElementCollector(famDoc)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (Element el in allElements)
            {
                if (el.Parameters == null) continue;

                foreach (Parameter p in el.Parameters)
                {
                    try
                    {
                        FamilyParameter? assoc = fm.GetAssociatedFamilyParameter(p);
                        if (assoc != null)
                            associated.Add(assoc.Definition.Name);
                    }
                    catch
                    {
                        // Some parameters/elements may not support association query
                    }
                }
            }

            return associated;
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
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Projection); if (w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Projection); } catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set projection line weight: {ex.Message}"); } catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set projection line weight: {ex.Message}"); } catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set projection line weight: {ex.Message}"); }
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Cut); if (w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Cut); } catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set cut line weight: {ex.Message}"); } catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set cut line weight: {ex.Message}"); } catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[BatchRenameExecutionService] Failed to set cut line weight: {ex.Message}"); }

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
                catch (ArgumentException)
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.");
                }
                catch (InvalidOperationException)
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.");
                }
                catch (RevitExceptions.InvalidOperationException)
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.");
                }

                return true;
            }
            catch (Exception ex) when (IsExpectedRenameException(ex))
            {
                logger.LogError($"Swap failed for {oldStyle.Name}: {ex.Message}");
                return false;
            }
        }

        private static bool IsExpectedRenameException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static void TryCloseFamilyDocument(Document? famDoc, string familyName, Logging.ILogger logger)
        {
            if (famDoc == null || !famDoc.IsValidObject)
            {
                return;
            }

            try
            {
                famDoc.Close(false);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}");
            }
        }
    }
}
