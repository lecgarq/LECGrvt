#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused family parameters from all families in the project.
    /// 
    /// A parameter is considered SAFE TO DELETE only if ALL of the following are true:
    ///   1. It is NOT a built-in parameter (Id.Value >= 0)
    ///   2. It is NOT a reporting parameter (IsReporting == false)
    ///   3. It has NO formula (Formula is null/empty)
    ///   4. It is NOT referenced by ANY other parameter's formula
    ///   5. It is NOT a dimension label (no Dimension.FamilyLabel points to it)
    ///   6. It is NOT associated to ANY element property (material, visibility, geometry, nested families)
    ///   7. It does NOT have meaningful values set across family types
    ///   8. It is NOT a shared parameter (shared params may be used in schedules/tags)
    /// </summary>
    public class PurgeParameterService : IPurgeParameterService
    {
        public int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning families for unused parameters...");

            int totalDeleted = 0;
            int familiesProcessed = 0;
            int familiesSkipped = 0;

            // Collect all families in the project
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            logCallback?.Invoke($"  Found {families.Count} families to scan.");

            foreach (Family family in families)
            {
                try
                {
                    // Skip non-editable families (in-place, system families)
                    if (!family.IsEditable)
                    {
                        familiesSkipped++;
                        continue;
                    }

                    int deletedInFamily = ProcessFamily(doc, family, logCallback);
                    totalDeleted += deletedInFamily;
                    familiesProcessed++;
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"  ⚠ Error processing family '{family.Name}': {ex.Message}");
                    familiesSkipped++;
                }
            }

            logCallback?.Invoke($"  Processed {familiesProcessed} families, skipped {familiesSkipped}.");
            logCallback?.Invoke($"  Deleted {totalDeleted} unused parameters.");
            return totalDeleted;
        }

        /// <summary>
        /// Opens a family document via EditFamily, finds unused parameters, removes them,
        /// reloads the family back, and closes the family document.
        /// </summary>
        private int ProcessFamily(Document projectDoc, Family family, Action<string>? logCallback)
        {
            Document? famDoc = projectDoc.EditFamily(family);
            if (famDoc == null)
            {
                logCallback?.Invoke($"  Could not open family '{family.Name}'.");
                return 0;
            }

            try
            {
                FamilyManager fm = famDoc.FamilyManager;
                if (fm == null)
                {
                    famDoc.Close(false);
                    return 0;
                }

                // Build safety sets
                var formulaReferencedParams = BuildFormulaReferencedSet(fm);
                var dimensionLabelParams = BuildDimensionLabelSet(famDoc);
                var elementAssociatedParams = BuildElementAssociationSet(famDoc, fm);
                var valueInUseParams = BuildValueInUseSet(fm);

                // Find parameters safe to delete
                var paramsToDelete = new List<FamilyParameter>();
                foreach (FamilyParameter fp in fm.Parameters)
                {
                    string reason = GetSkipReason(fp, formulaReferencedParams, dimensionLabelParams, elementAssociatedParams, valueInUseParams);
                    if (reason != null)
                    {
                        // Parameter is in use — skip
                        continue;
                    }

                    paramsToDelete.Add(fp);
                }

                if (paramsToDelete.Count == 0)
                {
                    famDoc.Close(false);
                    return 0;
                }

                // Delete unused parameters
                int deleted = 0;
                using (Transaction t = new Transaction(famDoc, "Purge Unused Parameters"))
                {
                    t.Start();

                    foreach (FamilyParameter fp in paramsToDelete)
                    {
                        try
                        {
                            string paramName = fp.Definition.Name;
                            fm.RemoveParameter(fp);
                            deleted++;
                            logCallback?.Invoke($"  Deleted: '{paramName}' from '{family.Name}'");
                        }
                        catch (Exception ex)
                        {
                            logCallback?.Invoke($"  Could not delete param '{fp.Definition.Name}' from '{family.Name}': {ex.Message}");
                        }
                    }

                    if (deleted > 0)
                        t.Commit();
                    else
                        t.RollBack();
                }

                // Reload family back into project if changes were made
                if (deleted > 0)
                    famDoc.LoadFamily(projectDoc, new OverwriteFamilyOption());

                famDoc.Close(false);
                return deleted;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  ⚠ Error in family '{family.Name}': {ex.Message}");
                try { famDoc.Close(false); } catch { /* swallow close errors */ }
                return 0;
            }
        }

        /// <summary>
        /// Returns null if the parameter is safe to delete, or a reason string if it should be kept.
        /// </summary>
        private string? GetSkipReason(
            FamilyParameter fp,
            HashSet<ElementId> formulaReferencedParams,
            HashSet<ElementId> dimensionLabelParams,
            HashSet<ElementId> elementAssociatedParams,
            HashSet<ElementId> valueInUseParams)
        {
            // 1. Built-in parameter
            if (fp.Id.Value < 0)
                return "built-in";

            // 2. Reporting parameter (dimension-driven)
            if (fp.IsReporting)
                return "reporting";

            // 3. Has a formula
            if (!string.IsNullOrEmpty(fp.Formula))
                return "has formula";

            // 4. Referenced by another parameter's formula
            if (formulaReferencedParams.Contains(fp.Id))
                return "referenced in formula";

            // 5. Is a dimension label
            if (dimensionLabelParams.Contains(fp.Id))
                return "dimension label";

            // 6. Associated with ANY element property (material, visibility, geometry, nested families)
            if (elementAssociatedParams.Contains(fp.Id))
                return "element association (material/visibility/geometry)";

            // 7. Has non-default values across family types
            if (valueInUseParams.Contains(fp.Id))
                return "has values set across types";

            // 8. Shared parameter (may be used in schedules, tags, filters in the project)
            if (fp.IsShared)
                return "shared parameter";

            return null; // Safe to delete
        }

        /// <summary>
        /// Build set of parameter IDs that are referenced in any other parameter's formula.
        /// For each parameter with a formula, parse the formula string and match
        /// against all known parameter names.
        /// </summary>
        private HashSet<ElementId> BuildFormulaReferencedSet(FamilyManager fm)
        {
            var referenced = new HashSet<ElementId>();

            // Build a name → Id lookup for all parameters
            var paramNameToId = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            foreach (FamilyParameter fp in fm.Parameters)
            {
                string name = fp.Definition.Name;
                if (!paramNameToId.ContainsKey(name))
                    paramNameToId[name] = fp.Id;
            }

            // Scan all formulas for parameter name references
            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (string.IsNullOrEmpty(fp.Formula)) continue;

                string formula = fp.Formula;
                foreach (var kvp in paramNameToId)
                {
                    // Check if the formula contains this parameter name
                    // Use case-insensitive contains as Revit formula references are by name
                    if (formula.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        referenced.Add(kvp.Value);
                    }
                }
            }

            return referenced;
        }

        /// <summary>
        /// Build set of parameter IDs that are used as dimension labels.
        /// Scans all Dimension elements in the family document.
        /// </summary>
        private HashSet<ElementId> BuildDimensionLabelSet(Document famDoc)
        {
            var labels = new HashSet<ElementId>();

            var dimensions = new FilteredElementCollector(famDoc)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>();

            foreach (Dimension dim in dimensions)
            {
                try
                {
                    FamilyParameter? label = dim.FamilyLabel;
                    if (label != null)
                    {
                        labels.Add(label.Id);
                    }
                }
                catch
                {
                    // Some dimensions may not support FamilyLabel — skip
                }
            }

            return labels;
        }

        /// <summary>
        /// Build set of parameter IDs that are associated with ANY element's properties.
        /// This scans ALL elements in the family document — not just nested FamilyInstances.
        /// Catches: material assignments, visibility toggles, geometry-driving params,
        /// reference plane associations, and nested family parameter mappings.
        /// </summary>
        private HashSet<ElementId> BuildElementAssociationSet(Document famDoc, FamilyManager fm)
        {
            var associated = new HashSet<ElementId>();

            // Scan ALL elements in the family document
            var allElements = new FilteredElementCollector(famDoc)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (Element el in allElements)
            {
                // Skip elements without parameters
                if (el.Parameters == null) continue;

                foreach (Parameter p in el.Parameters)
                {
                    try
                    {
                        FamilyParameter? assoc = fm.GetAssociatedFamilyParameter(p);
                        if (assoc != null)
                        {
                            associated.Add(assoc.Id);
                        }
                    }
                    catch
                    {
                        // Some parameters/elements may not support association query — skip
                    }
                }
            }

            return associated;
        }

        /// <summary>
        /// Build set of parameter IDs that have non-default values set across family types.
        /// If a parameter has any meaningful value in any type, it's considered in-use.
        /// </summary>
        private HashSet<ElementId> BuildValueInUseSet(FamilyManager fm)
        {
            var inUse = new HashSet<ElementId>();

            // Get all family types
            var types = fm.Types;
            if (types == null) return inUse;

            foreach (FamilyParameter fp in fm.Parameters)
            {
                // Skip built-in, they're already handled
                if (fp.Id.Value < 0) continue;

                foreach (FamilyType ft in types)
                {
                    try
                    {
                        if (ft.HasValue(fp))
                        {
                            // Parameter has a value set in this type — it's in use
                            inUse.Add(fp.Id);
                            break; // No need to check more types
                        }
                    }
                    catch
                    {
                        // Some type/param combos may throw — skip
                    }
                }
            }

            return inUse;
        }
    }
}
