using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused family parameters from all families in the project.
    ///
    /// A parameter is considered SAFE TO DELETE only if ALL of the following are true:
    ///   1. It is NOT a built-in parameter (Id.Value >= 0)
    ///   2. It is NOT a reporting parameter (IsReporting == false)
    ///   3. It is NOT a material-type parameter (always structural to family design)
    ///   4. It is NOT a third-party plugin parameter (name contains "Enscape")
    ///   5. It has NO formula (Formula is null/empty)
    ///   6. It is NOT referenced by ANY other parameter's formula
    ///   7. It is NOT a dimension label (no Dimension.FamilyLabel points to it)
    ///   8. It is NOT associated to ANY element property (material, visibility, geometry, nested families)
    ///   9. It does NOT have meaningful values set across family types
    ///  10. If shared: it is NOT registered at the project level (SharedParameterElement with matching GUID
    ///      indicates use in schedules, tags, or filters — those are KEPT)
    /// </summary>
    public class PurgeParameterService
    {
        private readonly ITransactionService _transactionService;
        private readonly IFamilyLoadOptionsFactory _loadOptionsFactory;

        public PurgeParameterService(ITransactionService transactionService, IFamilyLoadOptionsFactory loadOptionsFactory)
        {
            _transactionService = transactionService;
            _loadOptionsFactory = loadOptionsFactory;
        }

        public int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);

            logCallback?.Invoke("Scanning families for unused parameters...");

            int totalDeleted = 0;
            int familiesProcessed = 0;
            int familiesSkipped = 0;

            // Build project-level shared parameter GUIDs ONCE (used across all families)
            var projectSharedGuids = BuildProjectSharedParameterGuids(doc);
            logCallback?.Invoke($"  Found {projectSharedGuids.Count} shared parameters in use at project level (bindings, schedules, filters).");

            List<ProjectFamilyTarget> families = CollectProjectFamilies(doc);

            logCallback?.Invoke($"  Found {families.Count} families to scan.");

            foreach (ProjectFamilyTarget familyTarget in families)
            {
                try
                {
                    Family? family = doc.GetElement(familyTarget.Id) as Family;
                    if (family == null || !family.IsValidObject)
                    {
                        familiesSkipped++;
                        continue;
                    }

                    // Skip non-editable families (in-place, system families)
                    if (!family.IsEditable)
                    {
                        familiesSkipped++;
                        continue;
                    }

                    int deletedInFamily = ProcessFamily(doc, family, familyTarget.Name, projectSharedGuids, logCallback);
                    totalDeleted += deletedInFamily;
                    familiesProcessed++;
                }
                catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
                {
                    logCallback?.Invoke($"  WARNING Error processing family '{familyTarget.Name}': {ex.Message}");
                    familiesSkipped++;
                }
            }

            logCallback?.Invoke($"  Processed {familiesProcessed} families, skipped {familiesSkipped}.");
            logCallback?.Invoke($"  Deleted {totalDeleted} unused parameters.");
            return totalDeleted;
        }

        private static List<ProjectFamilyTarget> CollectProjectFamilies(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(family => new ProjectFamilyTarget(family.Id, family.Name))
                .ToList();
        }

        /// <summary>
        /// Opens a family document via EditFamily, finds unused parameters, removes them,
        /// reloads the family back, and closes the family document.
        /// </summary>
        private int ProcessFamily(Document projectDoc, Family family, string familyName, HashSet<Guid> projectSharedGuids, Action<string>? logCallback)
        {
            Document? famDoc = projectDoc.EditFamily(family);
            if (famDoc == null)
            {
                logCallback?.Invoke($"  Could not open family '{familyName}'.");
                return 0;
            }

            try
            {
                FamilyManager fm = famDoc.FamilyManager;
                if (fm == null)
                {
                    TryCloseFamilyDocument(famDoc, familyName, logCallback);
                    return 0;
                }

                // Build safety sets
                FamilyParameterUsageContext usageContext = BuildUsageContext(famDoc, fm);

                // Find parameters safe to delete
                var paramsToDelete = new List<FamilyParameter>();
                foreach (FamilyParameter fp in fm.Parameters)
                {
                    string? reason = GetSkipReason(
                        fp,
                        usageContext.FormulaReferencedParams,
                        usageContext.DimensionLabelParams,
                        usageContext.ElementAssociatedParams,
                        usageContext.ValueInUseParams,
                        projectSharedGuids);
                    if (reason != null)
                    {
                        // Parameter is in use — skip
                        continue;
                    }

                    paramsToDelete.Add(fp);
                }

                if (paramsToDelete.Count == 0)
                {
                    TryCloseFamilyDocument(famDoc, familyName, logCallback);
                    return 0;
                }

                // Delete unused parameters
                int deleted = DeleteParameters(famDoc, fm, paramsToDelete, familyName, logCallback);

                // Reload family back into project if changes were made.
                // DialogBoxShowing is handled at the PurgeCommand level (real UIApplication).
                // FailuresProcessing here handles failure messages during the internal LoadFamily transaction.
                if (deleted > 0)
                {
                    ReloadFamily(projectDoc, famDoc, familyName, logCallback);
                }

                TryCloseFamilyDocument(famDoc, familyName, logCallback);
                return deleted;
            }
            catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
            {
                logCallback?.Invoke($"  WARNING Error in family '{familyName}': {ex.Message}");
                TryCloseFamilyDocument(famDoc, familyName, logCallback);
                return 0;
            }
        }

        /// <summary>
        /// Returns null if the parameter is safe to delete, or a reason string if it should be kept.
        /// </summary>
        private static string? GetSkipReason(
            FamilyParameter fp,
            HashSet<ElementId> formulaReferencedParams,
            HashSet<ElementId> dimensionLabelParams,
            HashSet<ElementId> elementAssociatedParams,
            HashSet<ElementId> valueInUseParams,
            HashSet<Guid> projectSharedGuids)
        {
            // 1. Built-in parameter
            if (fp.Id.Value < 0)
                return "built-in";

            // 2. Reporting parameter (dimension-driven)
            if (fp.IsReporting)
                return "reporting";

            // 3. Material-type parameter — always structural to family design.
            // Material parameters are intentionally created even when no material is assigned yet.
            try
            {
                if (fp.Definition.GetDataType() == SpecTypeId.Reference.Material)
                    return "material-type parameter (structural to family)";
            }
            catch
            {
                // GetDataType may throw on some parameter types — continue with other checks
            }

            // 4. Third-party plugin parameter — preserve Enscape and similar renderer parameters
            if (fp.Definition.Name.Contains("Enscape", StringComparison.OrdinalIgnoreCase))
                return "third-party plugin parameter (Enscape)";

            // 5. Has a formula
            if (!string.IsNullOrEmpty(fp.Formula))
                return "has formula";

            // 6. Referenced by another parameter's formula
            if (formulaReferencedParams.Contains(fp.Id))
                return "referenced in formula";

            // 7. Is a dimension label
            if (dimensionLabelParams.Contains(fp.Id))
                return "dimension label";

            // 8. Associated with ANY element property (material, visibility, geometry, nested families)
            if (elementAssociatedParams.Contains(fp.Id))
                return "element association (material/visibility/geometry)";

            // 9. Has non-default values across family types
            if (valueInUseParams.Contains(fp.Id))
                return "has values set across types";

            // 10. Shared parameter registered at project level (used in schedules, tags, or filters)
            // Shared params NOT registered at project level are treated like regular params — deletable
            if (fp.IsShared && projectSharedGuids.Contains(fp.GUID))
                return "shared parameter used in project (schedules/tags/filters)";

            return null; // Safe to delete
        }

        private FamilyParameterUsageContext BuildUsageContext(Document famDoc, FamilyManager fm)
        {
            return new FamilyParameterUsageContext(
                BuildFormulaReferencedSet(fm),
                BuildDimensionLabelSet(famDoc),
                BuildElementAssociationSet(famDoc, fm),
                BuildValueInUseSet(fm));
        }

        private int DeleteParameters(
            Document famDoc,
            FamilyManager fm,
            List<FamilyParameter> paramsToDelete,
            string familyName,
            Action<string>? logCallback)
        {
            List<FamilyParameterTarget> parameterTargets = paramsToDelete
                .Select(fp => new FamilyParameterTarget(fp.Id, fp.Definition.Name))
                .ToList();

            int deleted = 0;
            _transactionService.RunConditional(famDoc, "Purge Unused Parameters", _ =>
            {
                foreach (FamilyParameterTarget parameterTarget in parameterTargets)
                {
                    FamilyParameter? currentParameter = FindFamilyParameter(fm, parameterTarget.Id, parameterTarget.Name);
                    if (currentParameter == null)
                    {
                        continue;
                    }

                    try
                    {
                        fm.RemoveParameter(currentParameter);
                        deleted++;
                        logCallback?.Invoke($"  Deleted: '{parameterTarget.Name}' from '{familyName}'");
                    }
                    catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
                    {
                        logCallback?.Invoke($"  Could not delete param '{parameterTarget.Name}' from '{familyName}': {ex.Message}");
                    }
                }

                return deleted > 0;
            });

            return deleted;
        }

        private static FamilyParameter? FindFamilyParameter(FamilyManager fm, ElementId id, string name)
        {
            foreach (FamilyParameter parameter in fm.Parameters)
            {
                if (parameter.Id == id)
                {
                    return parameter;
                }
            }

            foreach (FamilyParameter parameter in fm.Parameters)
            {
                if (string.Equals(parameter.Definition.Name, name, StringComparison.Ordinal))
                {
                    return parameter;
                }
            }

            return null;
        }

        private void ReloadFamily(Document projectDoc, Document famDoc, string familyName, Action<string>? logCallback)
        {
            EventHandler<Autodesk.Revit.DB.Events.FailuresProcessingEventArgs>? failureHandler = null;
            try
            {
                failureHandler = (sender, args) =>
                {
                    FailuresAccessor accessor = args.GetFailuresAccessor();
                    foreach (FailureMessageAccessor fma in accessor.GetFailureMessages())
                    {
                        if (fma.GetSeverity() == FailureSeverity.Warning)
                        {
                            accessor.DeleteWarning(fma);
                        }
                    }

                    if (accessor.GetFailureMessages().Count > 0)
                    {
                        args.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
                    }
                };

                projectDoc.Application.FailuresProcessing += failureHandler;
                // Preserve placed instances' parameter values — purge only removes unused parameters.
                famDoc.LoadFamily(projectDoc, _loadOptionsFactory.Create(overwriteParameterValues: false));
            }
            catch (Exception loadEx) when (IsExpectedFamilyPurgeException(loadEx))
            {
                logCallback?.Invoke($"  WARNING Could not reload '{familyName}': {loadEx.Message}");
            }
            finally
            {
                if (failureHandler != null)
                {
                    projectDoc.Application.FailuresProcessing -= failureHandler;
                }
            }
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
        /// Build set of parameter IDs that have meaningful non-default values across family types.
        /// Only considers a parameter "in use" if it has a non-zero/non-empty/non-invalid value,
        /// not just any value (HasValue returns true for defaults that Revit sets automatically).
        /// </summary>
        private static HashSet<ElementId> BuildValueInUseSet(FamilyManager fm)
        {
            var inUse = new HashSet<ElementId>();

            var types = fm.Types;
            if (types == null) return inUse;

            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (fp.Id.Value < 0) continue;

                foreach (FamilyType ft in types)
                {
                    try
                    {
                        if (!ft.HasValue(fp)) continue;

                        bool hasMeaningfulValue = fp.StorageType switch
                        {
                            StorageType.Double => ft.AsDouble(fp) is double d && Math.Abs(d) > 1e-9,
                            StorageType.Integer => ft.AsInteger(fp) is int i && i != 0,
                            StorageType.String => !string.IsNullOrEmpty(ft.AsString(fp)),
                            StorageType.ElementId => ft.AsElementId(fp) is ElementId eid && eid != ElementId.InvalidElementId,
                            _ => true
                        };

                        if (hasMeaningfulValue)
                        {
                            inUse.Add(fp.Id);
                            break;
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

        /// <summary>
        /// Build a set of shared parameter GUIDs that are actually USED at the project level.
        /// A SharedParameterElement exists for EVERY shared parameter loaded into the document —
        /// including ones that only arrived inside loaded families — so mere existence must not
        /// count as usage (it would keep every shared family parameter forever). Real usage is:
        /// category bindings (project parameters), schedule fields, and view filter rules.
        /// ponytail: tag-label usage isn't detected (requires opening every tag family);
        /// add that scan if users report tags going blank after a parameter purge.
        /// </summary>
        private static HashSet<Guid> BuildProjectSharedParameterGuids(Document projectDoc)
        {
            var usedParamIds = new HashSet<ElementId>();

            DefinitionBindingMapIterator iter = projectDoc.ParameterBindings.ForwardIterator();
            while (iter.MoveNext())
            {
                if (iter.Key is InternalDefinition internalDef && internalDef.Id != ElementId.InvalidElementId)
                {
                    usedParamIds.Add(internalDef.Id);
                }
            }

            foreach (ViewSchedule schedule in new FilteredElementCollector(projectDoc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>())
            {
                try
                {
                    ScheduleDefinition definition = schedule.Definition;
                    int fieldCount = definition.GetFieldCount();
                    for (int i = 0; i < fieldCount; i++)
                    {
                        usedParamIds.Add(definition.GetField(i).ParameterId);
                    }
                }
                catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
                {
                    // Some schedule types don't expose fields — skip.
                }
            }

            foreach (ParameterFilterElement filter in new FilteredElementCollector(projectDoc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>())
            {
                try
                {
                    usedParamIds.UnionWith(filter.GetElementFilterParameters());
                }
                catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
                {
                    // Filters without rules — skip.
                }
            }

            var guids = new HashSet<Guid>();
            foreach (SharedParameterElement sp in new FilteredElementCollector(projectDoc)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>())
            {
                if (usedParamIds.Contains(sp.Id))
                {
                    guids.Add(sp.GuidValue);
                }
            }

            return guids;
        }

        private static bool IsExpectedFamilyPurgeException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static void TryCloseFamilyDocument(Document? famDoc, string familyName, Action<string>? logCallback)
        {
            if (famDoc == null || !famDoc.IsValidObject)
            {
                return;
            }

            try
            {
                famDoc.Close(false);
            }
            catch (Exception ex) when (IsExpectedFamilyPurgeException(ex))
            {
                logCallback?.Invoke($"  Could not close family '{familyName}': {ex.Message}");
            }
        }

        private sealed class FamilyParameterUsageContext
        {
            public FamilyParameterUsageContext(
                HashSet<ElementId> formulaReferencedParams,
                HashSet<ElementId> dimensionLabelParams,
                HashSet<ElementId> elementAssociatedParams,
                HashSet<ElementId> valueInUseParams)
            {
                FormulaReferencedParams = formulaReferencedParams;
                DimensionLabelParams = dimensionLabelParams;
                ElementAssociatedParams = elementAssociatedParams;
                ValueInUseParams = valueInUseParams;
            }

            public HashSet<ElementId> FormulaReferencedParams { get; }
            public HashSet<ElementId> DimensionLabelParams { get; }
            public HashSet<ElementId> ElementAssociatedParams { get; }
            public HashSet<ElementId> ValueInUseParams { get; }
        }

        private sealed record ProjectFamilyTarget(ElementId Id, string Name);
        private sealed record FamilyParameterTarget(ElementId Id, string Name);
    }
}
