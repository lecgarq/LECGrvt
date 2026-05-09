using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitExceptions = Autodesk.Revit.Exceptions;
using LECG.Services.Interfaces;
using LECG.Models;
using LECG.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class FamilyConversionService : IFamilyConversionService
    {
        private readonly IFamilyTemplatePathService _templatePathService;
        private readonly IFamilySourceDocumentService _familySourceDocumentService;
        private readonly IFamilyConversionExecutionService _familyConversionExecutionService;
        private readonly IFamilyConversionNamingService _familyConversionNamingService;
        private readonly IFamilyConversionLoggingService _familyConversionLoggingService;
        private readonly IFamilyConversionFinalizeService _familyConversionFinalizeService;
        private readonly ITransactionService _transactionService;


        public FamilyConversionService(
            IFamilyTemplatePathService templatePathService,
            IFamilySourceDocumentService familySourceDocumentService,
            IFamilyConversionExecutionService familyConversionExecutionService,
            IFamilyConversionNamingService familyConversionNamingService,
            IFamilyConversionLoggingService familyConversionLoggingService,
            IFamilyConversionFinalizeService familyConversionFinalizeService,
            ITransactionService transactionService)
        {
            _templatePathService = templatePathService;
            _familySourceDocumentService = familySourceDocumentService;
            _familyConversionExecutionService = familyConversionExecutionService;
            _familyConversionNamingService = familyConversionNamingService;
            _familyConversionLoggingService = familyConversionLoggingService;
            _familyConversionFinalizeService = familyConversionFinalizeService;
            _transactionService = transactionService;
        }

        public void ConvertFamily(Document doc, FamilyInstance instance, string customName, string templatePath, bool isTemporary)
        {
            if (instance == null) return;
            ConvertFamilyBatch(doc, new[] { instance }, customName, templatePath, isTemporary, true);
        }

        public void ConvertFamilyBatch(Document doc, IEnumerable<FamilyInstance> instances, string customName, string templatePath, bool isTemporary, bool replaceInPlace, IProgressReporter? reporter = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            if (instances == null || !instances.Any()) return;

            int totalCount = instances.Count();
            int currentCount = 0;

            using (new ExecutionTimer($"Batch Conversion: {totalCount} instances"))
            {
                var instancesByFamily = instances.GroupBy(i => i.Symbol.Family.Id);

                foreach (var group in instancesByFamily)
                {
                    List<FamilyInstance> groupInstances = group.ToList();
                    currentCount = ProcessFamilyGroup(
                        doc,
                        groupInstances,
                        templatePath,
                        replaceInPlace,
                        totalCount,
                        currentCount,
                        reporter);
                }
                reporter?.Report("Batch Conversion Complete.", 100);
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }

        private int ProcessFamilyGroup(
            Document doc,
            IReadOnlyList<FamilyInstance> groupInstances,
            string templatePath,
            bool replaceInPlace,
            int totalCount,
            int currentCount,
            IProgressReporter? reporter)
        {
            FamilyInstance firstInstance = groupInstances[0];
            Family sourceFamily = firstInstance.Symbol.Family;
            string sourceFamilyName = sourceFamily.Name;
            string targetFamilyName = sourceFamilyName;
            string resolvedTemplatePath = ResolveTemplatePath(doc, templatePath, sourceFamily);

            using (new ExecutionTimer($"Family Group: {sourceFamilyName}"))
            {
                reporter?.Report($"Converting Family: {sourceFamilyName}...", (double)currentCount / totalCount * 100);
                _familyConversionLoggingService.LogStart(sourceFamilyName, targetFamilyName, resolvedTemplatePath, isTemporary: false);

                CapturedFamilyInstances capturedInstances = CaptureFamilyInstances(doc, sourceFamily.Id);
                Document? sourceFamilyDoc = _familySourceDocumentService.Open(doc, sourceFamily);
                if (sourceFamilyDoc == null)
                {
                    return currentCount + groupInstances.Count;
                }

                currentCount = ExecuteFamilyGroupConversion(
                    doc,
                    sourceFamily,
                    targetFamilyName,
                    resolvedTemplatePath,
                    replaceInPlace,
                    capturedInstances,
                    sourceFamilyDoc,
                    groupInstances.Count,
                    totalCount,
                    currentCount,
                    reporter);
            }

            return currentCount;
        }

        private int ExecuteFamilyGroupConversion(
            Document doc,
            Family sourceFamily,
            string targetFamilyName,
            string resolvedTemplatePath,
            bool replaceInPlace,
            CapturedFamilyInstances capturedInstances,
            Document sourceFamilyDoc,
            int groupInstanceCount,
            int totalCount,
            int currentCount,
            IProgressReporter? reporter)
        {
            Document? targetFamilyDoc = null;
            string tempFamilyPath = string.Empty;

            try
            {
                // 1. PRE-FLIGHT (read-only). Throws if any instance fails. No mutations yet.
                if (replaceInPlace)
                {
                    ValidatePreFlight(doc, capturedInstances.CapturedData);
                }

                // 2. LOAD NEW FAMILY (FamilyLoadOptionsFactory forces overwrite — safe with live instances).
                (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(
                    doc,
                    sourceFamily,
                    sourceFamilyDoc,
                    resolvedTemplatePath,
                    targetFamilyName);

                if (targetFamilyDoc == null)
                {
                    LECG.Services.Logging.Logger.Instance.Log("ERROR: targetFamilyDoc was null. Conversion failed internally; originals untouched.");
                    currentCount += groupInstanceCount;
                    return currentCount;
                }

                if (!replaceInPlace)
                {
                    return currentCount;  // No replacement requested.
                }

                // 3. VERIFY each captured instance has a name-matched symbol in the new family.
                //    Per CONTEXT.md §B: no first-symbol fallback. Refuse-all if any name missing.
                var symbolByName = new Dictionary<string, FamilySymbol>(StringComparer.Ordinal);
                foreach (FamilyInstanceData data in capturedInstances.CapturedData)
                {
                    string name = data.OriginalSymbolName!;  // pre-flight guaranteed non-null
                    if (symbolByName.ContainsKey(name)) continue;
                    FamilySymbol? sym = FindReplacementSymbolByName(doc, targetFamilyName, name);
                    if (sym == null)
                    {
                        LECG.Services.Logging.Logger.Instance.Log(
                            $"[PRE-FLIGHT] New family '{targetFamilyName}' lacks type '{name}'. Originals untouched.");
                        throw new InvalidOperationException(
                            $"Family conversion refused: new family '{targetFamilyName}' has no type named '{name}'.");
                    }
                    symbolByName[name] = sym;
                }

                // 4. DELETE ORIGINALS (only after load + symbol verification succeed).
                DeleteCapturedInstances(doc, capturedInstances.InstanceIds);
                LECG.Services.Logging.Logger.Instance.Log(
                    $"Cleared {capturedInstances.InstanceIds.Count} instances from project after successful family load.");

                // 5. PLACE NEW INSTANCES (per-instance symbol from the dict).
                currentCount = ReplaceCapturedInstances(
                    doc,
                    symbolByName,
                    capturedInstances.CapturedData,
                    totalCount,
                    currentCount,
                    reporter);
            }
            catch (Exception ex) when (IsExpectedGroupConversionException(ex))
            {
                _familyConversionLoggingService.LogCriticalError(ex.Message, ex.StackTrace ?? string.Empty);
                currentCount += groupInstanceCount;
            }
            finally
            {
                _familyConversionFinalizeService.Finalize(sourceFamilyDoc, targetFamilyDoc, tempFamilyPath, isTemporary: false);
            }

            return currentCount;
        }

        private string ResolveTemplatePath(Document doc, string templatePath, Family sourceFamily)
        {
            if (!string.IsNullOrEmpty(templatePath))
            {
                return templatePath;
            }

            return Configuration.RevitConstants.FindTemplate(@"English\LECG\-\LECG_070_GENERIC-MODELS.rft")
                ?? _templatePathService.GetTargetTemplatePath(doc.Application, sourceFamily.FamilyCategory);
        }

        private static CapturedFamilyInstances CaptureFamilyInstances(Document doc, ElementId familyId)
        {
            List<FamilyInstance> allInstancesOfFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(i => i.Symbol != null && i.Symbol.Family.Id == familyId)
                .ToList();

            List<FamilyInstanceData> capturedData = allInstancesOfFamily
                .Select(FamilyInstanceData.Capture)
                .ToList();

            List<ElementId> instanceIds = allInstancesOfFamily
                .Select(i => i.Id)
                .ToList();

            return new CapturedFamilyInstances(capturedData, instanceIds);
        }

        private void DeleteCapturedInstances(Document doc, IReadOnlyCollection<ElementId> oldInstanceIds)
        {
            _transactionService.Run(doc, "Delete Old Instances for Conversion", _ =>
            {
                foreach (ElementId id in oldInstanceIds)
                {
                    try
                    {
                        doc.Delete(id);
                    }
                    catch (ArgumentException ex)
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to delete instance {id}: {ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to delete instance {id}: {ex.Message}");
                    }
                }
            });
        }

        private static void ValidatePreFlight(
            Document doc,
            IReadOnlyList<FamilyInstanceData> capturedData)
        {
            var failures = new List<string>();

            foreach (FamilyInstanceData data in capturedData)
            {
                if (string.IsNullOrEmpty(data.OriginalSymbolName))
                {
                    failures.Add($"Instance at {data.LocationPoint}: original symbol name not captured.");
                    continue;
                }

                if (data.HostId != null && doc.GetElement(data.HostId) == null)
                {
                    failures.Add($"Instance at {data.LocationPoint}: host {data.HostId} not found in document.");
                }
            }

            if (failures.Count > 0)
            {
                foreach (string f in failures)
                    LECG.Services.Logging.Logger.Instance.Log($"[PRE-FLIGHT] {f}");
                throw new InvalidOperationException(
                    $"Pre-flight failed for family conversion ({failures.Count} issue(s)). Originals untouched.");
            }
        }

        private static FamilySymbol? FindReplacementSymbolByName(
            Document doc, string targetFamilyName, string? symbolName)
        {
            if (string.IsNullOrEmpty(symbolName)) return null;

            Family? newFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name == targetFamilyName);
            if (newFamily == null) return null;

            foreach (ElementId id in newFamily.GetFamilySymbolIds())
            {
                if (doc.GetElement(id) is FamilySymbol sym
                    && string.Equals(sym.Name, symbolName, StringComparison.Ordinal))
                    return sym;
            }
            return null;
        }

        private int ReplaceCapturedInstances(
            Document doc,
            IReadOnlyDictionary<string, FamilySymbol> symbolByName,
            IReadOnlyList<FamilyInstanceData> capturedData,
            int totalCount,
            int currentCount,
            IProgressReporter? reporter)
        {
            int updatedCount = currentCount;

            _transactionService.RunWithOptions(doc, "Replace Instances", _ =>
            {
                // Activate every symbol once.
                foreach (FamilySymbol sym in symbolByName.Values)
                {
                    if (!sym.IsActive) sym.Activate();
                }

                foreach (FamilyInstanceData data in capturedData)
                {
                    updatedCount++;
                    reporter?.Report($"Placing Instance {updatedCount} of {totalCount}...", (double)updatedCount / totalCount * 100);

                    if (string.IsNullOrEmpty(data.OriginalSymbolName)
                        || !symbolByName.TryGetValue(data.OriginalSymbolName, out FamilySymbol? sym))
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning(
                            $"[FamilyConversionService] No symbol available for original type '{data.OriginalSymbolName}'.");
                        continue;
                    }

                    FamilyInstance? newInstance = TryPlaceReplacementInstance(doc, sym, data);
                    if (newInstance == null) continue;

                    // Determine log location (ApplyCapturedInstanceData expects an XYZ for logging).
                    XYZ logLoc = data.LocationPoint
                        ?? data.LocationCurve?.GetEndPoint(0)
                        ?? XYZ.Zero;
                    ApplyCapturedInstanceData(data, newInstance, logLoc);
                }
            }, options => options.SetForcedModalHandling(false));

            return updatedCount;
        }

        private static void ApplyCapturedInstanceData(FamilyInstanceData data, FamilyInstance newInstance, XYZ location)
        {
            try
            {
                data.Apply(newInstance);
                LECG.Services.Logging.Logger.Instance.Log($"  [OK] Instance placed at ({location.X:F2}, {location.Y:F2}, {location.Z:F2})");
            }
            catch (ArgumentException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to apply captured state to {newInstance.Id}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to apply captured state to {newInstance.Id}: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to apply captured state to {newInstance.Id}: {ex.Message}");
            }
        }

        private static bool IsExpectedGroupConversionException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static Level? ResolvePlacementLevel(Document doc, ElementId levelId)
        {
            return doc.GetElement(levelId) as Level
                ?? doc.ActiveView?.GenLevel
                ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
        }

        private static FamilyInstance? TryPlaceReplacementInstance(
            Document doc, FamilySymbol newSymbol, FamilyInstanceData data)
        {
            try
            {
                // Branch 1: curve-driven instance — preserve LocationCurve.
                if (data.LocationCurve != null)
                {
                    Level? lev = ResolvePlacementLevel(doc, data.LevelId);
                    if (lev == null)
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning(
                            "[FamilyConversionService] Skipping curve instance — no valid level.");
                        return null;
                    }
                    return doc.Create.NewFamilyInstance(
                        data.LocationCurve,
                        newSymbol,
                        lev,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }

                // Branch 2: hosted point instance — preserve HostId.
                if (data.LocationPoint != null && data.HostId != null)
                {
                    Element? host = doc.GetElement(data.HostId);
                    if (host == null)
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning(
                            $"[FamilyConversionService] Host {data.HostId} not found at placement time (pre-flight stale?).");
                        return null;
                    }
                    return doc.Create.NewFamilyInstance(
                        data.LocationPoint,
                        newSymbol,
                        host,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }

                // Branch 3: free-standing point with level (existing behavior).
                if (data.LocationPoint != null)
                {
                    Level? lev = ResolvePlacementLevel(doc, data.LevelId);
                    if (lev != null)
                    {
                        return doc.Create.NewFamilyInstance(
                            data.LocationPoint,
                            newSymbol,
                            lev,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    }
                    return doc.Create.NewFamilyInstance(
                        data.LocationPoint,
                        newSymbol,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }

                LECG.Services.Logging.Logger.Instance.LogWarning(
                    "[FamilyConversionService] Captured instance has neither LocationPoint nor LocationCurve.");
                return null;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning(
                    $"[FamilyConversionService] Placement failed: {ex.Message}");
                return null;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning(
                    $"[FamilyConversionService] Placement failed: {ex.Message}");
                return null;
            }
        }

        private sealed record CapturedFamilyInstances(
            IReadOnlyList<FamilyInstanceData> CapturedData,
            IReadOnlyCollection<ElementId> InstanceIds);
    }
}
