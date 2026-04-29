using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitExceptions = Autodesk.Revit.Exceptions;
using LECG.Core;
using LECG.Core.Rename;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LECG.Commands
{
    /// <summary>
    /// Moves all shared and family parameters that have a formula to the "Other" group.
    /// Works on both type and instance parameters.
    /// - In a family document: processes the active family directly.
    /// - In a project document: processes all editable families in the project.
    /// Built-in (native Revit) parameters are skipped — their group cannot be changed.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class FormulaAutoGroupingCommand : RevitCommand
    {
        private static bool s_projectRunActive;

        protected override string? TransactionName => null;

        private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            e.OverrideResult(2);
            string detail = e is TaskDialogShowingEventArgs td ? td.Message : e.DialogId ?? "unknown";
            Logger.Instance.Log($"  Auto-dismissed dialog: {detail}");
        }

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            UIApplication app = uiDoc.Application;

            ShowLogWindow("Formula Auto Grouping");
            if (!TryResolveTargetGroup(GroupTypeId.General, out TargetGroupContext? targetGroup, out string? resolutionError))
            {
                UpdateProgress(100, "Failed");
                Log($"ERROR: {resolutionError}");
                return;
            }

            TargetGroupContext resolvedTargetGroup = targetGroup!;
            Log($"Resolved runtime label '{resolvedTargetGroup.ResolvedLabel}' for {nameof(GroupTypeId)}.{nameof(GroupTypeId.General)}. Using the group id as the source of truth.");

            if (doc.IsFamilyDocument)
            {
                try
                {
                    app.DialogBoxShowing += OnDialogShowing;
                    ExecuteInFamilyDocument(doc, resolvedTargetGroup);
                }
                finally
                {
                    app.DialogBoxShowing -= OnDialogShowing;
                }

                UpdateProgress(100, "Complete");
                Log("Formula Auto Grouping Complete.");
                return;
            }

            ExecuteInProjectDocument(app, doc, resolvedTargetGroup);
        }

        /// <summary>
        /// Active document is a family — process its parameters directly (no EditFamily/LoadFamily needed).
        /// </summary>
        private void ExecuteInFamilyDocument(Document famDoc, TargetGroupContext targetGroup)
        {
            FamilyManager fm = famDoc.FamilyManager;
            if (fm == null)
            {
                Log("No FamilyManager available.");
                return;
            }

            var paramsToChange = CollectFormulaRelatedParams(fm, targetGroup.Id);
            if (paramsToChange.Count == 0)
            {
                Log($"No direct-formula parameters need regrouping for target group '{targetGroup.ResolvedLabel}'.");
                return;
            }

            Log($"Found {paramsToChange.Count} direct-formula candidate{(paramsToChange.Count == 1 ? string.Empty : "s")} for target group '{targetGroup.ResolvedLabel}'.");

            int changed = 0;
            using (Transaction t = new Transaction(famDoc, "Formula Auto Grouping"))
            {
                t.Start();
                try
                {
                    changed = MoveParamsToGroup(famDoc, fm, paramsToChange, targetGroup, famDoc.Title);
                    if (changed > 0)
                    {
                        t.Commit();
                    }
                    else
                    {
                        t.RollBack();
                    }
                }
                catch (UnsupportedGroupChangeException ex)
                {
                    t.RollBack();
                    Log($"No changes applied to '{famDoc.Title}': supported {changed}/{paramsToChange.Count} candidates for target group '{targetGroup.ResolvedLabel}'. {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    Log($"No changes applied to '{famDoc.Title}': {ex.Message}");
                    return;
                }
            }

            Log($"Moved {changed}/{paramsToChange.Count} direct-formula candidate{(changed == 1 ? string.Empty : "s")} to verified group '{targetGroup.ResolvedLabel}'.");
        }

        /// <summary>
        /// Active document is a project — iterate all editable families, EditFamily/LoadFamily each.
        /// </summary>
        private void ExecuteInProjectDocument(UIApplication app, Document doc, TargetGroupContext targetGroup)
        {
            if (s_projectRunActive)
            {
                Log("Formula Auto Grouping is already running.");
                return;
            }

            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable)
                .Select(f => new ProjectFamilyTarget(f.Id, f.Name))
                .ToList();

            Log($"Found {families.Count} editable families to scan.");
            if (families.Count == 0)
            {
                UpdateProgress(100, "Complete");
                Log("Formula Auto Grouping Complete.");
                return;
            }

            var loadOptionsFactory = ServiceLocator.GetRequiredService<IFamilyLoadOptionsFactory>();
            s_projectRunActive = true;
            Log("Processing families in short slices...");
            UpdateProgress(0, "Queued");

            var job = new ProjectFormulaAutoGroupingJob(this, app, doc, families, targetGroup, loadOptionsFactory);
            new RevitIdlingRunner(app, job).Start();
        }

        private sealed class ProjectFormulaAutoGroupingJob : IRevitSliceJob
        {
            private readonly FormulaAutoGroupingCommand _command;
            private readonly UIApplication _application;
            private readonly Document _projectDocument;
            private readonly IReadOnlyList<ProjectFamilyTarget> _families;
            private readonly TargetGroupContext _targetGroup;
            private readonly IFamilyLoadOptionsFactory _loadOptionsFactory;

            private int _familyIndex;
            private int _totalChanged;
            private int _familiesModified;
            private int _unsupportedFamilies;
            private int _openOrLoadFailures;
            private int _failedFamilies;

            public ProjectFormulaAutoGroupingJob(
                FormulaAutoGroupingCommand command,
                UIApplication application,
                Document projectDocument,
                IReadOnlyList<ProjectFamilyTarget> families,
                TargetGroupContext targetGroup,
                IFamilyLoadOptionsFactory loadOptionsFactory)
            {
                _command = command ?? throw new ArgumentNullException(nameof(command));
                _application = application ?? throw new ArgumentNullException(nameof(application));
                _projectDocument = projectDocument ?? throw new ArgumentNullException(nameof(projectDocument));
                _families = families ?? throw new ArgumentNullException(nameof(families));
                _targetGroup = targetGroup;
                _loadOptionsFactory = loadOptionsFactory ?? throw new ArgumentNullException(nameof(loadOptionsFactory));

                _application.DialogBoxShowing += OnDialogShowing;
            }

            public bool ExecuteNextSlice()
            {
                if (_familyIndex >= _families.Count)
                {
                    return false;
                }

                ProjectFamilyTarget family = _families[_familyIndex];
                _command.UpdateProgress(CalculateOverallProgress(), $"Processing {family.Name}...");

                ProjectFamilyResult result = _command.ProcessProjectFamily(_projectDocument, family, _targetGroup, _loadOptionsFactory);
                if (result.Outcome == ProjectFamilyOutcome.Changed)
                {
                    _totalChanged += result.ChangedCount;
                    _familiesModified++;
                }
                else if (result.Outcome == ProjectFamilyOutcome.Unsupported)
                {
                    _unsupportedFamilies++;
                }
                else if (result.Outcome == ProjectFamilyOutcome.OpenOrLoadFailed)
                {
                    _openOrLoadFailures++;
                }
                else if (result.Outcome == ProjectFamilyOutcome.Failed)
                {
                    _failedFamilies++;
                }

                _familyIndex++;
                _command.UpdateProgress(CalculateOverallProgress(), _familyIndex < _families.Count ? "Advancing to next family..." : "Finishing...");
                return _familyIndex < _families.Count;
            }

            public void Complete()
            {
                _command.UpdateProgress(100, "Complete");
                _command.Log("");
                _command.Log($"Moved {_totalChanged} direct-formula parameters to verified group '{_targetGroup.ResolvedLabel}' across {_familiesModified} families.");
                _command.Log($"Skipped {_unsupportedFamilies} unsupported families.");
                _command.Log($"Open/load failures: {_openOrLoadFailures}. Other failures: {_failedFamilies}.");
                _command.Log("Formula Auto Grouping Complete.");
            }

            public void Fail(Exception ex)
            {
                _command.UpdateProgress(100, "Failed");
                _command.Log($"ERROR: {ex.Message}");
            }

            public void Dispose()
            {
                _application.DialogBoxShowing -= OnDialogShowing;
                s_projectRunActive = false;
            }

            private double CalculateOverallProgress()
            {
                if (_families.Count == 0)
                {
                    return 100;
                }

                return Math.Min(99, _familyIndex / (double)_families.Count * 100d);
            }
        }

        private ProjectFamilyResult ProcessProjectFamily(Document projectDoc, ProjectFamilyTarget family, TargetGroupContext targetGroup, IFamilyLoadOptionsFactory loadOptionsFactory)
        {
            Family? currentFamily = ResolveEditableProjectFamily(projectDoc, family);
            if (currentFamily == null)
            {
                Log($"  Could not resolve editable family '{family.Name}' in the project document.");
                return new ProjectFamilyResult(ProjectFamilyOutcome.OpenOrLoadFailed);
            }

            Document? famDoc = TryOpenFamilyDocument(projectDoc, currentFamily, family.Name);
            if (famDoc == null) return new ProjectFamilyResult(ProjectFamilyOutcome.OpenOrLoadFailed);

            try
            {
                FamilyManager? fm = famDoc.FamilyManager;
                if (fm == null)
                {
                    Log($"  No FamilyManager available for '{family.Name}'.");
                    return new ProjectFamilyResult(ProjectFamilyOutcome.Failed);
                }

                var paramsToChange = CollectFormulaRelatedParams(fm, targetGroup.Id);
                if (paramsToChange.Count == 0) return new ProjectFamilyResult(ProjectFamilyOutcome.NoCandidates);

                Log($"Processing '{family.Name}' ({paramsToChange.Count} candidate{(paramsToChange.Count == 1 ? string.Empty : "s")}, target '{targetGroup.ResolvedLabel}').");

                int changed = 0;
                using (Transaction t = new Transaction(famDoc, "Formula Auto Grouping"))
                {
                    t.Start();
                    try
                    {
                        changed = MoveParamsToGroup(famDoc, fm, paramsToChange, targetGroup, family.Name);
                        if (changed > 0)
                        {
                            t.Commit();
                        }
                        else
                        {
                            t.RollBack();
                        }
                    }
                    catch (UnsupportedGroupChangeException ex)
                    {
                        t.RollBack();
                        Log($"  Skipped '{family.Name}': supported {changed}/{paramsToChange.Count} candidates for target group '{targetGroup.ResolvedLabel}'. {ex.Message}");
                        return new ProjectFamilyResult(ProjectFamilyOutcome.Unsupported);
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        Log($"  Error in '{family.Name}': {ex.Message}");
                        return new ProjectFamilyResult(ProjectFamilyOutcome.Failed);
                    }
                }

                if (changed > 0)
                {
                    if (!TryReloadFamily(projectDoc, famDoc, family.Name, loadOptionsFactory))
                    {
                        Log($"  No changes applied to '{family.Name}' because the family could not be reloaded.");
                        return new ProjectFamilyResult(ProjectFamilyOutcome.OpenOrLoadFailed);
                    }

                    TryCloseFamilyDocument(famDoc, family.Name);
                    famDoc = null;

                    if (!VerifyReloadedFamily(projectDoc, family, paramsToChange, targetGroup, out string? verificationError, out bool verificationUnsupported))
                    {
                        if (verificationUnsupported)
                        {
                            Log($"  Skipped '{family.Name}': moved {changed}/{paramsToChange.Count} candidates inside the family document, but reload verification did not persist target group '{targetGroup.ResolvedLabel}'. {verificationError}");
                        }
                        else
                        {
                            Log($"  Error in '{family.Name}': {verificationError}");
                        }
                        return new ProjectFamilyResult(verificationUnsupported ? ProjectFamilyOutcome.Unsupported : ProjectFamilyOutcome.OpenOrLoadFailed);
                    }

                    Log($"Completed '{family.Name}': moved {changed}/{paramsToChange.Count} candidates to verified group '{targetGroup.ResolvedLabel}'.");
                }

                return new ProjectFamilyResult(ProjectFamilyOutcome.Changed, changed);
            }
            finally
            {
                if (famDoc != null)
                {
                    TryCloseFamilyDocument(famDoc, family.Name);
                }
            }
        }

        private Document? TryOpenFamilyDocument(Document projectDoc, Family family, string familyName)
        {
            try
            {
                return projectDoc.EditFamily(family);
            }
            catch (ArgumentException ex)
            {
                Log($"  Could not open family '{familyName}': {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                Log($"  Could not open family '{familyName}': {ex.Message}");
                return null;
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Log($"  Could not open family '{familyName}': {ex.Message}");
                return null;
            }
        }

        private bool TryReloadFamily(Document projectDoc, Document famDoc, string familyName, IFamilyLoadOptionsFactory loadOptionsFactory)
        {
            try
            {
                IFamilySaveLoadService familySaveLoadService = ServiceLocator.GetRequiredService<IFamilySaveLoadService>();
                familySaveLoadService.SaveAndLoad(projectDoc, famDoc, familyName);
                return true;
            }
            catch (ArgumentException ex)
            {
                Log($"  Could not reload '{familyName}': {ex.Message}");
                return false;
            }
            catch (RevitExceptions.ArgumentException ex)
            {
                Log($"  Could not reload '{familyName}': {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Log($"  Could not reload '{familyName}': {ex.Message}");
                return false;
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Log($"  Could not reload '{familyName}': {ex.Message}");
                return false;
            }
        }

        private void TryCloseFamilyDocument(Document famDoc, string familyName)
        {
            try
            {
                famDoc.Close(false);
            }
            catch (InvalidOperationException ex)
            {
                Log($"  Could not close family document '{familyName}': {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Log($"  Could not close family document '{familyName}': {ex.Message}");
            }
        }

        private static bool IsExpectedFamilyProcessingException(Exception ex)
        {
            return ex is ArgumentException
                or RevitExceptions.ArgumentException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }

        private enum ProjectFamilyOutcome
        {
            NoCandidates,
            Changed,
            Unsupported,
            OpenOrLoadFailed,
            Failed
        }

        private sealed record ProjectFamilyResult(ProjectFamilyOutcome Outcome, int ChangedCount = 0);

        private sealed record TargetGroupContext(ForgeTypeId Id, string ResolvedLabel);

        private sealed record ProjectFamilyTarget(ElementId Id, string Name);

        private sealed record FamilyParameterTarget(long IdValue, string Name);

        private sealed class UnsupportedGroupChangeException : InvalidOperationException
        {
            public UnsupportedGroupChangeException(string message) : base(message)
            {
            }
        }

        /// <summary>
        /// Collect all non-built-in parameters whose own Formula is non-empty.
        /// Parameters already in the target group are excluded.
        /// </summary>
        private static List<FamilyParameterTarget> CollectFormulaRelatedParams(
            FamilyManager fm, ForgeTypeId targetGroup)
        {
            var filtered = new List<FamilyParameterTarget>();
            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (fp.Id.Value < 0) continue;
                if (fp.Definition.GetGroupTypeId() == targetGroup) continue;
                if (!TryGetFormula(fp, out string? formula) || string.IsNullOrWhiteSpace(formula)) continue;

                filtered.Add(new FamilyParameterTarget(fp.Id.Value, fp.Definition.Name));
            }
            return filtered;
        }

        /// <summary>
        /// Move parameters to the target group in place.
        /// This preserves parameter identity, formulas, labels, and associations.
        /// </summary>
        private int MoveParamsToGroup(Document familyDoc, FamilyManager fm, IReadOnlyList<FamilyParameterTarget> parameters,
            TargetGroupContext targetGroup, string familyName)
        {
            if (parameters.Count == 0) return 0;

            int changed = 0;

            foreach (FamilyParameterTarget parameter in parameters)
            {
                string name = parameter.Name;
                FamilyParameter? currentParameter = FindParamByIdOrName(fm, parameter.IdValue, name);
                if (currentParameter == null)
                {
                    throw new InvalidOperationException($"Could not resolve '{name}' before regrouping.");
                }

                if (currentParameter.Definition.GetGroupTypeId() == targetGroup.Id)
                {
                    continue;
                }

                using (var sub = new SubTransaction(familyDoc))
                {
                    sub.Start();
                    if (TryMoveParameterToGroup(familyDoc, fm, currentParameter, targetGroup, out string? error))
                    {
                        sub.Commit();
                        changed++;
                    }
                    else
                    {
                        sub.RollBack();
                        Log($"  Skipped '{name}': {error ?? "move failed"}");
                    }
                }
            }

            return changed;
        }

        private void EnsureParametersPersistInGroup(
            FamilyManager familyManager,
            IReadOnlyList<FamilyParameterTarget> parameters,
            TargetGroupContext targetGroup,
            string familyName)
        {
            foreach (FamilyParameterTarget parameter in parameters)
            {
                FamilyParameter? currentParameter = FindParamByIdOrName(familyManager, parameter.IdValue, parameter.Name);
                if (currentParameter == null)
                {
                    throw new UnsupportedGroupChangeException($"Parameter '{parameter.Name}' could not be found for verification in '{familyName}'.");
                }

                if (currentParameter.Definition.GetGroupTypeId() != targetGroup.Id)
                {
                    string currentGroupLabel = DescribeGroup(currentParameter.Definition.GetGroupTypeId());
                    throw new UnsupportedGroupChangeException($"Parameter '{parameter.Name}' resolved to runtime group '{currentGroupLabel}' instead of verified target '{targetGroup.ResolvedLabel}' in '{familyName}'.");
                }
            }
        }

        private bool VerifyReloadedFamily(
            Document projectDoc,
            ProjectFamilyTarget family,
            IReadOnlyList<FamilyParameterTarget> parameters,
            TargetGroupContext targetGroup,
            out string? error,
            out bool unsupported)
        {
            Family? currentFamily = ResolveEditableProjectFamily(projectDoc, family);
            if (currentFamily == null)
            {
                error = $"Could not reopen editable family '{family.Name}' for post-reload verification.";
                unsupported = false;
                return false;
            }

            Document? verificationDoc = TryOpenFamilyDocument(projectDoc, currentFamily, family.Name);
            if (verificationDoc == null)
            {
                error = $"Could not reopen '{family.Name}' for post-reload verification.";
                unsupported = false;
                return false;
            }

            try
            {
                FamilyManager? familyManager = verificationDoc.FamilyManager;
                if (familyManager == null)
                {
                    error = $"No FamilyManager available while verifying '{family.Name}'.";
                    unsupported = false;
                    return false;
                }

                EnsureParametersPersistInGroup(familyManager, parameters, targetGroup, family.Name);
                error = null;
                unsupported = false;
                return true;
            }
            catch (UnsupportedGroupChangeException ex)
            {
                error = ex.Message;
                unsupported = true;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                unsupported = false;
                return false;
            }
            finally
            {
                TryCloseFamilyDocument(verificationDoc, family.Name);
            }
        }

        private static bool TrySetParameterGroup(FamilyParameter parameter, ForgeTypeId targetGroup, out string? error)
        {
            if (parameter.Definition is not InternalDefinition internalDefinition)
            {
                error = "parameter definition does not support in-place group changes";
                return false;
            }

            try
            {
                internalDefinition.SetGroupTypeId(targetGroup);
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                error = ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static Family? ResolveEditableProjectFamily(Document projectDoc, ProjectFamilyTarget familyTarget)
        {
            if (projectDoc.GetElement(familyTarget.Id) is Family familyById && familyById.IsEditable)
            {
                return familyById;
            }

            return new FilteredElementCollector(projectDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f =>
                    f.IsEditable &&
                    string.Equals(f.Name, familyTarget.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryMoveParameterToGroup(
            Document familyDoc,
            FamilyManager familyManager,
            FamilyParameter parameter,
            TargetGroupContext targetGroup,
            out string? error)
        {
            if (parameter.IsShared)
            {
                return TryReplaceSharedParameterGroup(familyDoc, familyManager, parameter, targetGroup, out error);
            }

            return TrySetParameterGroup(parameter, targetGroup.Id, out error);
        }

        private static bool TryReplaceSharedParameterGroup(
            Document familyDoc,
            FamilyManager familyManager,
            FamilyParameter parameter,
            TargetGroupContext targetGroup,
            out string? error)
        {
            string parameterName = parameter.Definition.Name;
            bool isInstance = parameter.IsInstance;
            Guid parameterGuid = parameter.GUID;
            int originalAssociationCount = 0;
            bool hasAssociationCount = TryGetAssociatedParameterCount(parameter, out originalAssociationCount);
            string? originalFormula = null;

            if (TryGetFormula(parameter, out string? formula) && !string.IsNullOrWhiteSpace(formula))
            {
                originalFormula = formula;
            }

            if (!TryResolveSharedParameterDefinition(familyDoc, parameter, out ExternalDefinition? externalDefinition, out error))
            {
                return false;
            }

            // Clear formulas on OTHER parameters that reference this parameter's name.
            // ReplaceParameter throws InvalidOperationException if any active formula references
            // the parameter being replaced. We clear them first and restore after.
            var formulasToRestore = new List<(FamilyParameter fp, string formula)>();
            foreach (FamilyParameter fp in familyManager.Parameters)
            {
                if (fp.Id.Value == parameter.Id.Value) continue; // skip the parameter being replaced
                if (!TryGetFormula(fp, out string? refFormula) || string.IsNullOrWhiteSpace(refFormula)) continue;
                if (!FormulaNameUpdater.ContainsReference(refFormula, parameterName)) continue;
                formulasToRestore.Add((fp, refFormula));
                TrySetFormula(familyManager, fp, string.Empty, out _); // clear the reference — ignore failure (best effort)
            }

            FamilyParameter replacementParameter;
            try
            {
                replacementParameter = familyManager.ReplaceParameter(parameter, externalDefinition!, targetGroup.Id, isInstance);
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                error = ex.Message;
                return false;
            }

            if (!replacementParameter.IsShared)
            {
                error = "ReplaceParameter returned a non-shared parameter";
                return false;
            }

            if (replacementParameter.GUID != parameterGuid)
            {
                error = $"ReplaceParameter changed the shared parameter GUID for '{parameterName}'.";
                return false;
            }

            if (replacementParameter.Definition.GetGroupTypeId() != targetGroup.Id)
            {
                error = "shared parameter replace did not persist target group";
                return false;
            }

            // Restore formulas on other parameters that were cleared before ReplaceParameter.
            // Use FindParamByName because the FamilyParameter object references may be stale
            // after ReplaceParameter — look up by name to get the live object.
            if (formulasToRestore.Count > 0)
            {
                _ = EnsureCurrentType(familyManager); // best effort — log nothing, TrySetFormula handles null CurrentType
                foreach (var (fp, savedFormula) in formulasToRestore)
                {
                    FamilyParameter? current = FindParamByName(familyManager, fp.Definition.Name);
                    if (current != null)
                    {
                        TrySetFormula(familyManager, current, savedFormula, out _); // best effort — do not fail the move
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(originalFormula))
            {
                if (!TryGetFormula(replacementParameter, out string? currentFormula)
                    || !string.Equals(currentFormula?.Trim(), originalFormula.Trim(), StringComparison.Ordinal))
                {
                    if (!TrySetFormula(familyManager, replacementParameter, originalFormula, out string? formulaError))
                    {
                        error = $"formula was not preserved: {formulaError}";
                        return false;
                    }

                    if (!TryGetFormula(replacementParameter, out currentFormula)
                        || !string.Equals(currentFormula?.Trim(), originalFormula.Trim(), StringComparison.Ordinal))
                    {
                        error = "formula was not preserved after replacing the shared parameter";
                        return false;
                    }
                }
            }

            if (hasAssociationCount
                && TryGetAssociatedParameterCount(replacementParameter, out int replacementAssociationCount)
                && replacementAssociationCount != originalAssociationCount)
            {
                error = $"associated parameter count changed from {originalAssociationCount} to {replacementAssociationCount}";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryResolveTargetGroup(
            ForgeTypeId candidateGroup,
            out TargetGroupContext? targetGroup,
            out string? error)
        {
            if (!TryGetGroupLabel(candidateGroup, out string resolvedLabel, out string? labelError))
            {
                targetGroup = null;
                error = $"Could not resolve runtime label for {nameof(GroupTypeId)}.{nameof(GroupTypeId.General)}: {labelError}";
                return false;
            }

            targetGroup = new TargetGroupContext(candidateGroup, resolvedLabel);
            error = null;
            return true;
        }

        private static bool TryGetGroupLabel(ForgeTypeId groupTypeId, out string label, out string? error)
        {
            try
            {
                label = LabelUtils.GetLabelForGroup(groupTypeId);
                if (string.IsNullOrWhiteSpace(label))
                {
                    error = "Revit returned an empty label.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                label = string.Empty;
                error = ex.Message;
                return false;
            }
        }

        private static string DescribeGroup(ForgeTypeId groupTypeId)
        {
            return TryGetGroupLabel(groupTypeId, out string label, out _) ? label : groupTypeId.ToString() ?? "unknown";
        }

        private static bool TryGetFormula(FamilyParameter parameter, out string? formula)
        {
            try
            {
                formula = parameter.Formula;
                return true;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                formula = null;
                return false;
            }
        }

        private static bool TrySetFormula(FamilyManager familyManager, FamilyParameter parameter, string formula, out string? error)
        {
            if (!EnsureCurrentType(familyManager))
            {
                error = "no family types exist — SetFormula requires a current type";
                return false;
            }

            try
            {
                familyManager.SetFormula(parameter, formula);
                error = null;
                return true;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryGetAssociatedParameterCount(FamilyParameter parameter, out int count)
        {
            try
            {
                count = parameter.AssociatedParameters.Cast<Parameter>().Count();
                return true;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                count = 0;
                return false;
            }
        }

        private static bool TryResolveSharedParameterDefinition(
            Document familyDoc,
            FamilyParameter parameter,
            out ExternalDefinition? externalDefinition,
            out string? error)
        {
            if (parameter.Definition is ExternalDefinition directExternalDefinition)
            {
                externalDefinition = directExternalDefinition;
                error = null;
                return true;
            }

            Autodesk.Revit.ApplicationServices.Application application = familyDoc.Application;
            string sharedParameterPath = application.SharedParametersFilename;
            if (string.IsNullOrWhiteSpace(sharedParameterPath))
            {
                externalDefinition = null;
                error = "no shared parameter file is configured";
                return false;
            }

            DefinitionFile? definitionFile;
            try
            {
                definitionFile = application.OpenSharedParameterFile();
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                externalDefinition = null;
                error = $"could not open shared parameter file '{sharedParameterPath}': {ex.Message}";
                return false;
            }

            if (definitionFile == null)
            {
                externalDefinition = null;
                error = $"could not open shared parameter file '{sharedParameterPath}'";
                return false;
            }

            Guid parameterGuid = parameter.GUID;
            foreach (DefinitionGroup definitionGroup in definitionFile.Groups)
            {
                foreach (Definition definition in definitionGroup.Definitions)
                {
                    if (definition is ExternalDefinition candidate && candidate.GUID == parameterGuid)
                    {
                        externalDefinition = candidate;
                        error = null;
                        return true;
                    }
                }
            }

            externalDefinition = null;
            error = $"shared parameter GUID '{parameterGuid}' was not found in '{sharedParameterPath}'";
            return false;
        }

        private static bool TryGetFamilyTypeValue(FamilyType familyType, FamilyParameter parameter, StorageType storageType, out object? value)
        {
            try
            {
                if (!familyType.HasValue(parameter))
                {
                    value = null;
                    return false;
                }

                value = storageType switch
                {
                    StorageType.Double => (object?)familyType.AsDouble(parameter),
                    StorageType.Integer => familyType.AsInteger(parameter),
                    StorageType.String => familyType.AsString(parameter),
                    StorageType.ElementId => familyType.AsElementId(parameter),
                    _ => null
                };

                return value != null;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                value = null;
                return false;
            }
        }

        private static bool TrySetCurrentType(FamilyManager familyManager, FamilyType familyType)
        {
            try
            {
                familyManager.CurrentType = familyType;
                return true;
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
                return false;
            }
        }

        private static bool EnsureCurrentType(FamilyManager fm)
        {
            if (fm.CurrentType != null) return true;
            foreach (FamilyType ft in fm.Types)
            {
                return TrySetCurrentType(fm, ft);
            }
            return false; // no types exist
        }

        private static void TrySetFamilyTypeValue(FamilyManager familyManager, FamilyParameter parameter, StorageType storageType, object value)
        {
            try
            {
                switch (storageType)
                {
                    case StorageType.Double when value is double d:
                        familyManager.Set(parameter, d);
                        break;
                    case StorageType.Integer when value is int i:
                        familyManager.Set(parameter, i);
                        break;
                    case StorageType.String when value is string s:
                        familyManager.Set(parameter, s);
                        break;
                    case StorageType.ElementId when value is ElementId id:
                        familyManager.Set(parameter, id);
                        break;
                }
            }
            catch (Exception ex) when (IsExpectedFormulaGroupingException(ex))
            {
            }
        }

        private static bool IsExpectedFormulaGroupingException(Exception ex)
        {
            return ex is ArgumentException
                || ex is RevitExceptions.ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static FamilyParameter? FindParamByName(FamilyManager fm, string name)
        {
            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (string.Equals(fp.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    return fp;
            }
            return null;
        }

        private static FamilyParameter? FindParamByIdOrName(FamilyManager fm, long idValue, string name)
        {
            FamilyParameter? fallback = null;

            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (fp.Id.Value == idValue)
                    return fp;

                if (fallback == null && string.Equals(fp.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    fallback = fp;
            }

            return fallback;
        }
    }
}
