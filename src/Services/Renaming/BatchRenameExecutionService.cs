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
        private readonly IFormulaUpdateService _formulaUpdateService;

        public BatchRenameExecutionService(ITransactionService transactionService, IFamilyLoadOptionsFactory loadOptionsFactory, IFormulaUpdateService formulaUpdateService)
        {
            _transactionService = transactionService;
            _loadOptionsFactory = loadOptionsFactory;
            _formulaUpdateService = formulaUpdateService ?? throw new ArgumentNullException(nameof(formulaUpdateService));
        }

        public int ExecuteBatchRename(Document doc, List<ElementRowViewModel> items, Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(logger);

            return ExecuteBatchRename(doc, items, logger, new LegacyProgressReporter(logger, onProgress));
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

            logger.Log($"Starting batch rename for {total} items ({standardItems.Count} standard, {familyItems.Count} family parameters)...", "BatchRename");

            // Pre-flight dry-run: evaluate skip conditions for standard items BEFORE the rename
            // transaction. This is a read-only walk — no Revit state is mutated here.
            // Skipped rows have Status set, IsRenameable=false, IsChecked=false so the commit
            // pass skips them automatically via the existing `if (!item.IsChecked) continue` guard.
            if (standardItems.Count > 0)
            {
                var claimedNewNames = new HashSet<string>(StringComparer.Ordinal);
                ApplyPreFlightSkipReasons(
                    standardItems,
                    row =>
                    {
                        ElementId id = new ElementId(row.Id);
                        Element? el = doc.GetElement(id);
                        if (el == null) return null;
                        return GetStandardItemSkipReason(el, row.NewValue, doc, claimedNewNames);
                    },
                    logger);
            }

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
                                                logger.LogSuccess($"Renamed (via Swap) '{item.OriginalValue}' to '{item.NewValue}'", "BatchRename");
                                            }
                                            else
                                            {
                                                logger.LogError($"Skipped '{item.OriginalValue}': API restricted & Swap failed.", "BatchRename");
                                            }
                                        }
                                        else
                                        {
                                            logger.LogError($"Skipped '{item.OriginalValue}': Renaming this specific Object Style is restricted by the Revit API.", "BatchRename");
                                        }
                                        continue;
                                    }
                                    catch (ArgumentException innerEx)
                                    {
                                        logger.LogError($"Failed to rename style '{item.OriginalValue}': {innerEx.Message}", "BatchRename");
                                        continue;
                                    }
                                    catch (InvalidOperationException innerEx)
                                    {
                                        logger.LogError($"Failed to rename style '{item.OriginalValue}': {innerEx.Message}", "BatchRename");
                                        continue;
                                    }
                                }
                                else
                                {
                                    el.Name = item.NewValue;
                                    count++;
                                }

                                logger.LogSuccess($"Renamed '{item.OriginalValue}' to '{item.NewValue}'", "BatchRename");
                            }
                            catch (Exception ex) when (IsExpectedRenameException(ex))
                            {
                                logger.LogError($"ERROR renaming {item.Name}: {ex.Message}", "BatchRename");
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

                foreach (var kvp in byFamily)
                {
                    // Polish #3 (Phase 5): progress denominator unified with standard loop — see 05-03-PLAN
                    // Advance current by the number of items in this family group so progress is
                    // monotonic 0→100 across both standard and family loops.
                    current += kvp.Value.Count;
                    double percent = (double)current / total * 100;

                    ElementId familyId = new ElementId(kvp.Key);
                    Element el = doc.GetElement(familyId);

                    if (el is not Family family)
                    {
                        foreach (var item in kvp.Value)
                            logger.LogError($"Skipped: Element {kvp.Key} is not a Family (type={el?.GetType().Name ?? "null"}).", "BatchRename");
                        continue;
                    }

                    string familyName = family.Name;

                    // EditFamily throws for families Revit will not open: in-place families
                    // ("This family is in-place and is not supported for editing") and
                    // non-editable ones such as system panels ("This family is not editable").
                    // Both are knowable up front, so report them as skips rather than letting
                    // them surface as errors from a failed edit.
                    if (family.IsInPlace)
                    {
                        foreach (var item in kvp.Value)
                            logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': in-place families cannot be edited.", "BatchRename");
                        continue;
                    }

                    if (!family.IsEditable)
                    {
                        foreach (var item in kvp.Value)
                            logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': family is not editable.", "BatchRename");
                        continue;
                    }

                    reporter.Report($"Processing Family '{familyName}'...", percent);

                    Document? famDoc = null;
                    try
                    {
                        famDoc = doc.EditFamily(family);
                        if (famDoc == null)
                        {
                            logger.LogError($"Could not open family document for '{familyName}'.", "BatchRename");
                            continue;
                        }

                        // Pre-validate: build safety sets to detect parameters that could break the family
                        FamilyManager preCheckMgr = famDoc.FamilyManager;
                        var dimensionLabels = BuildDimensionLabelNames(famDoc);
                        var dimensionsByName = BuildDimensionsByLabelName(famDoc);
                        var formulaReferenced = BuildFormulaReferencedNames(preCheckMgr);
                        var elementAssociated = BuildElementAssociationNames(famDoc, preCheckMgr);

                        int renamedInFamily = 0;
                        bool committed = _transactionService.RunConditional(famDoc, "Rename Parameters", _ =>
                        {
                            FamilyManager mgr = famDoc.FamilyManager;

                            renamedInFamily = RenameFamilyParameters(
                                mgr,
                                famDoc,
                                kvp.Value,
                                familyName,
                                dimensionLabels,
                                dimensionsByName,
                                formulaReferenced,
                                elementAssociated,
                                logger);
                            return renamedInFamily > 0;
                        });

                        // Polish #1 (Phase 5): count incremented only after committed observation — see 05-03-PLAN
                        count = AccumulateCommittedFamilyCount(committed, renamedInFamily, count);

                        // Reload ONCE after all parameters are renamed in this family
                        if (committed)
                            famDoc.LoadFamily(doc, _loadOptionsFactory.Create());

                        famDoc.Close(false);
                    }
                    catch (Exception ex) when (IsExpectedRenameException(ex))
                    {
                        logger.LogError($"Failed processing family '{familyName}': {ex.Message}", "BatchRename");
                    }
                    finally
                    {
                        TryCloseFamilyDocument(famDoc, familyName, logger);
                    }
                }
            }

            logger.LogSuccess($"Batch rename complete. Modified {count} elements.", "BatchRename");
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

        private int RenameFamilyParameters(
            FamilyManager manager,
            Document famDoc,
            List<ElementRowViewModel> items,
            string familyName,
            HashSet<string> dimensionLabels,
            Dictionary<string, List<Dimension>> dimensionsByName,
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
                    logger.Log($"Skipped: Param '{item.OriginalValue}' not found in family '{familyName}'.", "BatchRename");
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
                    logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': {skipReason}", "BatchRename");
                    continue;
                }

                // Per-param SubTransaction: rename + formula-update + dimension-label reassignment
                // is atomic per parameter. On exception the SubTransaction rolls back; the outer
                // transaction (and other params) are unaffected. renamedCount is incremented ONLY
                // after confirmed Commit.
                SubTransaction? subTx = null;
                try
                {
                    subTx = new SubTransaction(famDoc);
                    subTx.Start();

                    manager.RenameParameter(paramToRename, item.NewValue);

                    // Formula-update loop: rewrite any formula referencing the old name.
                    // Only enter the loop if the parameter is known to be formula-referenced
                    // (opt-in via formulaReferenced set — performance guard, no behaviour change).
                    int formulaCount = 0;
                    if (formulaReferenced.Contains(item.OriginalValue))
                    {
                        var paramFormulas = new List<(string name, string formula)>();
                        foreach (FamilyParameter fp in manager.Parameters)
                        {
                            if (!string.IsNullOrEmpty(fp.Formula))
                                paramFormulas.Add((fp.Definition.Name, fp.Formula));
                        }

                        var updates = CollectFormulaUpdates(paramFormulas, item.OriginalValue, item.NewValue, _formulaUpdateService);
                        foreach (var (targetName, updatedFormula) in updates)
                        {
                            FamilyParameter? targetParam = FindFamilyParameterByName(manager, targetName);
                            if (targetParam != null)
                            {
                                manager.SetFormula(targetParam, updatedFormula);
                                formulaCount++;
                            }
                        }
                    }

                    // Dimension-label reassignment loop: for each Dimension whose FamilyLabel
                    // pointed at the old parameter name, reassign to the freshly renamed param.
                    // PITFALL 2 GUARD: refetch the renamed param reference via FindFamilyParameterByName
                    // BEFORE setting dim.FamilyLabel — the old paramToRename reference is stale after
                    // RenameParameter.
                    int dimCount = 0;
                    if (dimensionsByName.TryGetValue(item.OriginalValue, out var dims) && dims.Count > 0)
                    {
                        // PITFALL 2 GUARD: refetch the renamed param reference AFTER RenameParameter —
                        // the original paramToRename reference is stale after rename.
                        FamilyParameter? renamedRef = FindFamilyParameterByName(manager, item.NewValue);
                        if (renamedRef != null)
                        {
                            // Polish #2 (Phase 5 C1 follow-up): null-clear FamilyLabel before reassign — see 05-03-PLAN
                            var reassignPairs = new List<(Action clear, Action assign)>(dims.Count);
                            foreach (Dimension dim in dims)
                            {
                                Dimension capturedDim = dim;
                                FamilyParameter capturedRef = renamedRef;
                                reassignPairs.Add((
                                    clear: () => { capturedDim.FamilyLabel = null; },
                                    assign: () => { capturedDim.FamilyLabel = capturedRef; }
                                ));
                            }

                            ExecuteDimensionReassignments(reassignPairs, item.OriginalValue, item.NewValue, out dimCount);
                        }
                    }

                    subTx.Commit();

                    // Increment ONLY after confirmed commit (Pitfall 3 guard)
                    renamedCount++;
                    LogRenameSuccess(logger, item.OriginalValue, item.NewValue, formulaCount, dimCount);
                }
                catch (Exception ex)
                {
                    if (subTx != null && subTx.IsValidObject &&
                        subTx.GetStatus() == TransactionStatus.Started)
                    {
                        subTx.RollBack();
                    }

                    logger.LogWarning($"Skipped '{item.OriginalValue}' in '{familyName}': {ex.Message}", "BatchRename");
                    // Do NOT increment renamedCount — the SubTransaction was rolled back
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

        /// <summary>
        /// Pure-data helper: returns the updated committed-family rename count.
        /// If <paramref name="committed"/> is true, adds <paramref name="renamedInFamily"/> to
        /// <paramref name="currentCount"/>; otherwise returns <paramref name="currentCount"/> unchanged.
        /// Extracted so that the count increment happens only AFTER the RunConditional result is
        /// observed, preventing phantom count inflation on rollback (Polish #1, Phase 5).
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        // Polish #1 (Phase 5): count incremented only after committed observation — see 05-03-PLAN
        internal static int AccumulateCommittedFamilyCount(bool committed, int renamedInFamily, int currentCount)
            => committed ? currentCount + renamedInFamily : currentCount;

        /// <summary>
        /// Pure-data helper: collect formula updates for a rename operation.
        /// For each parameter whose formula contains a reference to <paramref name="oldName"/>,
        /// calls <paramref name="service"/>.UpdateFormula and returns the (paramName, updatedFormula) pairs.
        /// No Revit API objects are touched — suitable for unit testing without RevitAPI.dll.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static List<(string name, string updatedFormula)> CollectFormulaUpdates(
            IEnumerable<(string name, string formula)> parameters,
            string oldName,
            string newName,
            IFormulaUpdateService service)
        {
            var result = new List<(string name, string updatedFormula)>();
            foreach (var (name, formula) in parameters)
            {
                if (string.IsNullOrEmpty(formula)) continue;
                if (!FormulaNameUpdater.ContainsReference(formula, oldName)) continue;

                string updated = service.UpdateFormula(formula, oldName, newName);
                result.Add((name, updated));
            }

            return result;
        }

        /// <summary>
        /// Emits a success log line for a rename operation, including formula and/or dimension
        /// count suffixes when references were rewritten. The four branches are:
        /// both &gt; 0: "(updated F formulas, D dimension labels)"
        /// formulas only: "(updated F formulas)"
        /// dimensions only: "(updated D dimension labels)"
        /// neither: no suffix
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static void LogRenameSuccess(Logging.ILogger logger, string oldName, string newName, int formulaCount, int dimCount = 0)
        {
            logger.LogSuccess(FormatSafeRenameLog(oldName, newName, formulaCount, dimCount), "BatchRename");
        }

        /// <summary>
        /// Pure-data helper: formats the success log message for a safe rename, composing formula
        /// and dimension label update counts. Returns "Renamed '{old}' to '{new}'" when both counts
        /// are zero; adds count suffixes otherwise. Centralized for testability.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static string FormatSafeRenameLog(string oldName, string newName, int formulaCount, int dimCount)
        {
            if (formulaCount == 0 && dimCount == 0)
                return $"Renamed '{oldName}' to '{newName}'";
            if (formulaCount > 0 && dimCount > 0)
                return $"Renamed '{oldName}' to '{newName}' (updated {formulaCount} formulas, {dimCount} dimension labels)";
            if (formulaCount > 0)
                return $"Renamed '{oldName}' to '{newName}' (updated {formulaCount} formulas)";
            return $"Renamed '{oldName}' to '{newName}' (updated {dimCount} dimension labels)";
        }

        /// <summary>
        /// Pure-data helper: executes a list of pre-built dimension reassignment actions and counts
        /// the successful ones. If any action throws, the exception propagates to roll back the
        /// enclosing SubTransaction. Used inside the per-param SubTransaction after RenameParameter.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static void ExecuteDimensionReassignments(
            IReadOnlyList<Action> reassignActions,
            string oldName,
            string newName,
            out int dimCount)
        {
            dimCount = 0;
            foreach (Action action in reassignActions)
            {
                action(); // throws → propagates to SubTransaction rollback
                dimCount++;
            }
        }

        /// <summary>
        /// Pair-action overload: executes a list of (clear, assign) action pairs, running clear()
        /// before assign() per pair. Enables the null-clear → reassign pattern required by
        /// Dimension.FamilyLabel (C1 follow-up from Phase 4 verification).
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        // Polish #2 (Phase 5 C1 follow-up): null-clear FamilyLabel before reassign — see 05-03-PLAN
        internal static void ExecuteDimensionReassignments(
            IReadOnlyList<(Action clear, Action assign)> pairs,
            string oldName,
            string newName,
            out int dimCount)
        {
            dimCount = 0;
            if (pairs == null) return;
            foreach (var (clear, assign) in pairs)
            {
                clear();
                assign();
                dimCount++;
            }
        }

        /// <summary>
        /// Test-accessible wrapper around GroupCheckedFamilyParameterItems.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static Dictionary<long, List<ElementRowViewModel>> GroupCheckedFamilyParameterItemsForTest(
            List<ElementRowViewModel> familyItems)
            => GroupCheckedFamilyParameterItems(familyItems);

        /// <summary>
        /// Pure-data helper: produces a monotonically increasing 0→100 progress sequence for a mixed
        /// batch of <paramref name="standardCount"/> standard items followed by family groups.
        /// Each family group contributes one progress step weighted by its row count.
        /// The final value is guaranteed to be 100.0. Empty input returns an empty sequence.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        // Polish #3 (Phase 5): progress denominator unified with standard loop — see 05-03-PLAN
        internal static IReadOnlyList<double> BuildProgressSequence(
            int standardCount,
            IReadOnlyList<int> familyGroupRowCounts)
        {
            if (familyGroupRowCounts == null) familyGroupRowCounts = Array.Empty<int>();
            int total = standardCount + familyGroupRowCounts.Sum();
            if (total == 0) return Array.Empty<double>();

            var result = new List<double>(capacity: standardCount + familyGroupRowCounts.Count);
            int current = 0;
            for (int i = 0; i < standardCount; i++)
            {
                current++;
                result.Add((double)current / total * 100.0);
            }
            foreach (int rowCount in familyGroupRowCounts)
            {
                current += rowCount;
                result.Add((double)current / total * 100.0);
            }
            return result;
        }

        /// <summary>
        /// Pre-flight dry-run loop for standard items. Runs OUTSIDE the main rename transaction.
        /// For each checked row, calls <paramref name="getSkipReason"/> to evaluate skip conditions.
        /// If a skip reason is returned, sets <c>row.Status</c>, <c>row.IsRenameable = false</c>,
        /// <c>row.IsChecked = false</c>, and emits one <c>LogWarning</c> per skipped row.
        /// If the row is renameable, adds <c>row.NewValue</c> to the cross-batch collision set
        /// by letting the caller's <paramref name="getSkipReason"/> resolve it (Pitfall 5 guard).
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static void ApplyPreFlightSkipReasons(
            IEnumerable<ElementRowViewModel> rows,
            Func<ElementRowViewModel, string?> getSkipReason,
            Logging.ILogger logger)
        {
            foreach (var row in rows)
            {
                if (!row.IsChecked) continue;

                string? reason = getSkipReason(row);
                if (reason != null)
                {
                    row.Status = reason;
                    row.IsRenameable = false;
                    row.IsChecked = false;
                    logger.LogWarning($"Skipped '{row.OriginalValue}' ({row.Type}): {reason}", "BatchRename");
                }
            }
        }

        /// <summary>
        /// Returns the user-facing skip reason ONLY for the three remaining un-rename-able conditions:
        /// built-in, reporting, name-conflict. Formula-referenced, dimension-label, and element-associated
        /// params are renamed via the safe-rename path in plans 04-03/04-04.
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

            // Build the set of existing param names (excluding the param being renamed)
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FamilyParameter existing in mgr.Parameters)
            {
                if (existing.Id != fp.Id)
                    existingNames.Add(existing.Definition.Name);
            }

            // TODO 04-03: hand formulaReferenced set to safe-rename loop; TODO 04-04: hand dimensionLabels set to dimension reassignment loop
            return EvaluateFamilyParamSkipReason(
                paramIdValue: fp.Id.Value,
                isReporting: fp.IsReporting,
                paramName: name,
                newName: newName,
                existingParamNames: existingNames,
                formulaReferenced: formulaReferenced,
                dimensionLabels: dimensionLabels,
                elementAssociated: elementAssociated);
        }

        /// <summary>
        /// Pure-data skip evaluation for FamilyParameter rename — no Revit API objects required.
        /// Evaluates only the three remaining skip conditions: built-in, reporting, name-conflict.
        /// Formula-referenced, dimension-label, and element-associated are intentionally NOT checked
        /// (they are handled by the safe-rename path in plans 04-03/04-04).
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static string? EvaluateFamilyParamSkipReason(
            long paramIdValue,
            bool isReporting,
            string paramName,
            string newName,
            IEnumerable<string> existingParamNames,
            HashSet<string> formulaReferenced,
            HashSet<string> dimensionLabels,
            HashSet<string> elementAssociated)
        {
            // Built-in parameters cannot be renamed
            if (paramIdValue < 0)
                return "built-in parameter (cannot rename)";

            // Reporting parameters are dimension-driven — renaming could break references
            if (isReporting)
                return "reporting parameter (dimension-driven)";

            // Check if the new name conflicts with an existing parameter name
            foreach (string existing in existingParamNames)
            {
                if (existing.Equals(newName, StringComparison.OrdinalIgnoreCase))
                    return $"new name '{newName}' conflicts with existing parameter";
            }

            // NOTE: formula-referenced, dimension-label, and element-associated are NO LONGER skip
            // conditions after Phase 4 narrowing. They are passed through to the safe-rename paths
            // in plans 04-03 (formula) and 04-04 (dimension labels).
            // Suppressing unused-parameter warnings by referencing them:
            _ = formulaReferenced;
            _ = dimensionLabels;
            _ = elementAssociated;

            return null; // Safe to rename
        }

        /// <summary>
        /// Returns the user-facing skip reason for standard-item (non-FamilyParameter) rename conditions.
        /// Detects: cross-batch collision, system family, name conflict in scope, sheet number locked,
        /// and read-only/built-in element types. Returns null for freely-renameable rows.
        /// </summary>
        internal static string? GetStandardItemSkipReason(
            Element el,
            string newValue,
            Document doc,
            HashSet<string> claimedNewNames)
        {
            // Cross-batch collision check first (Pitfall 5)
            if (claimedNewNames.Contains(newValue))
                return $"name '{newValue}' already claimed by another row in this batch";

            // System family check — deferred to Plan 04-01 follow-up; default to false.
            // The pure-data EvaluateStandardItemSkipReason supports the flag when populated.
            bool isSystemFamily = false;

            // Read-only / built-in type check (probe via name comparison with existing elements of same type)
            bool nameAlreadyInScope = false;
            try
            {
                nameAlreadyInScope = new FilteredElementCollector(doc)
                    .OfClass(el.GetType())
                    .Cast<Element>()
                    .Any(e => e.Id != el.Id &&
                              string.Equals(e.Name, newValue, StringComparison.Ordinal));
            }
            catch
            {
                // If collector throws, default to no conflict
            }

            // Sheet number locked check
            bool isSheetWithLockedNumber = false;
            if (el is ViewSheet vs)
            {
                // Probe: attempt to read the numbering scheme via a probe SubTransaction
                isSheetWithLockedNumber = IsSheetNumberLocked(doc, vs, newValue);
            }

            // Read-only check: element types that reject Name assignment
            bool isReadOnly = el is GraphicsStyle;

            return EvaluateStandardItemSkipReason(
                isReadOnly: isReadOnly,
                isSystemFamily: isSystemFamily,
                nameAlreadyInScope: nameAlreadyInScope,
                isSheetWithLockedNumber: isSheetWithLockedNumber,
                newValue: newValue,
                claimedNewNames: claimedNewNames);
        }

        /// <summary>
        /// Pure-data skip evaluation for standard items — no Revit API objects required.
        /// Internal for unit testing via InternalsVisibleTo.
        /// </summary>
        internal static string? EvaluateStandardItemSkipReason(
            bool isReadOnly,
            bool isSystemFamily,
            bool nameAlreadyInScope,
            bool isSheetWithLockedNumber,
            string newValue,
            HashSet<string> claimedNewNames)
        {
            // Cross-batch collision (Pitfall 5) — checked first for determinism
            if (claimedNewNames.Contains(newValue))
                return $"name '{newValue}' already claimed by another row in this batch";

            // System family — names are restricted
            if (isSystemFamily)
                return "system family — names are restricted";

            // Name conflict in same scope
            if (nameAlreadyInScope)
                return $"name '{newValue}' already in use in scope";

            // Sheet number locked or restricted by numbering scheme
            if (isSheetWithLockedNumber)
                return "sheet number locked or restricted by numbering scheme";

            // Read-only or built-in type
            if (isReadOnly)
                return "read-only or built-in type";

            return null; // Freely renameable
        }

        /// <summary>
        /// Probes whether a ViewSheet's sheet number is locked or restricted.
        /// Uses a probe SubTransaction that is always rolled back (never committed).
        /// </summary>
        private static bool IsSheetNumberLocked(Document doc, ViewSheet vs, string newSheetNumber)
        {
            SubTransaction? probeTx = null;
            try
            {
                probeTx = new SubTransaction(doc);
                probeTx.Start();
                vs.SheetNumber = newSheetNumber;
                // If we get here, the number was accepted
                return false;
            }
            catch (RevitExceptions.InvalidOperationException)
            {
                return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            finally
            {
                try
                {
                    if (probeTx != null && probeTx.IsValidObject &&
                        probeTx.GetStatus() == TransactionStatus.Started)
                    {
                        probeTx.RollBack();
                    }
                }
                catch { /* probe cleanup — best effort */ }
            }
        }

        /// <summary>
        /// Build set of parameter names that are used as dimension labels in the family.
        /// Preserved intact — still used by GetRenameSkipReason for skip-condition detection.
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
        /// Build a dictionary of Dimension objects grouped by their current FamilyLabel parameter name.
        /// Dimensions without a FamilyLabel (null) are omitted. Used in the per-param SubTransaction
        /// to reassign dimension labels after a rename (REQ-03). Key comparison is Ordinal.
        /// </summary>
        private static Dictionary<string, List<Dimension>> BuildDimensionsByLabelName(Document famDoc)
        {
            var result = new Dictionary<string, List<Dimension>>(StringComparer.Ordinal);
            foreach (Dimension dim in new FilteredElementCollector(famDoc).OfClass(typeof(Dimension)).Cast<Dimension>())
            {
                FamilyParameter? label = dim.FamilyLabel;
                if (label == null) continue;
                string name = label.Definition.Name;
                if (!result.TryGetValue(name, out var list)) { list = new List<Dimension>(); result[name] = list; }
                list.Add(dim);
            }
            return result;
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
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Projection); if (w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Projection); } catch (ArgumentException ex) { logger.LogWarning($"Failed to set projection line weight: {ex.Message}", scope: "BatchRename"); } catch (InvalidOperationException ex) { logger.LogWarning($"Failed to set projection line weight: {ex.Message}", scope: "BatchRename"); } catch (RevitExceptions.InvalidOperationException ex) { logger.LogWarning($"Failed to set projection line weight: {ex.Message}", scope: "BatchRename"); }
                try { int? w = oldCat.GetLineWeight(GraphicsStyleType.Cut); if (w.HasValue) newCat.SetLineWeight(w.Value, GraphicsStyleType.Cut); } catch (ArgumentException ex) { logger.LogWarning($"Failed to set cut line weight: {ex.Message}", scope: "BatchRename"); } catch (InvalidOperationException ex) { logger.LogWarning($"Failed to set cut line weight: {ex.Message}", scope: "BatchRename"); } catch (RevitExceptions.InvalidOperationException ex) { logger.LogWarning($"Failed to set cut line weight: {ex.Message}", scope: "BatchRename"); }

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
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.", "BatchRename");
                }
                catch (InvalidOperationException)
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.", "BatchRename");
                }
                catch (RevitExceptions.InvalidOperationException)
                {
                    logger.Log($"Warning: deeply swapped '{oldStyle.Name}' to '{newName}' but could not delete original.", "BatchRename");
                }

                return true;
            }
            catch (Exception ex) when (IsExpectedRenameException(ex))
            {
                logger.LogError($"Swap failed for {oldStyle.Name}: {ex.Message}", "BatchRename");
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
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}", "BatchRename");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}", "BatchRename");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                logger.LogWarning($"Could not close family '{familyName}': {ex.Message}", "BatchRename");
            }
        }
    }
}
