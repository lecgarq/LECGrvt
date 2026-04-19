using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitExceptions = Autodesk.Revit.Exceptions;
using LECG.Services.Interfaces;
using LECG.Models;
using LECG.Utils;
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
                if (replaceInPlace)
                {
                    DeleteCapturedInstances(doc, capturedInstances.InstanceIds);
                    LECG.Services.Logging.Logger.Instance.Log($"Cleared {capturedInstances.InstanceIds.Count} instances from project to unlock native conversion.");
                }

                (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(
                    doc,
                    sourceFamily,
                    sourceFamilyDoc,
                    resolvedTemplatePath,
                    targetFamilyName);

                if (targetFamilyDoc != null && replaceInPlace)
                {
                    FamilySymbol? newSymbol = FindReplacementSymbol(doc, targetFamilyName);
                    if (newSymbol != null)
                    {
                        currentCount = ReplaceCapturedInstances(
                            doc,
                            newSymbol,
                            capturedInstances.CapturedData,
                            totalCount,
                            currentCount,
                            reporter);
                    }
                }
                else if (targetFamilyDoc == null)
                {
                    LECG.Services.Logging.Logger.Instance.Log("ERROR: targetFamilyDoc was null. Conversion failed internally.");
                    currentCount += groupInstanceCount;
                }
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

        private static FamilySymbol? FindReplacementSymbol(Document doc, string targetFamilyName)
        {
            Family? newFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name == targetFamilyName);

            if (newFamily == null)
            {
                return null;
            }

            ICollection<ElementId> symbolIds = newFamily.GetFamilySymbolIds();
            if (!symbolIds.Any())
            {
                return null;
            }

            return doc.GetElement(symbolIds.First()) as FamilySymbol;
        }

        private int ReplaceCapturedInstances(
            Document doc,
            FamilySymbol newSymbol,
            IReadOnlyList<FamilyInstanceData> capturedData,
            int totalCount,
            int currentCount,
            IProgressReporter? reporter)
        {
            int updatedCount = currentCount;

            _transactionService.RunWithOptions(doc, "Replace Instances", _ =>
            {
                if (!newSymbol.IsActive)
                {
                    newSymbol.Activate();
                }

                foreach (FamilyInstanceData data in capturedData)
                {
                    updatedCount++;
                    reporter?.Report($"Placing Instance {updatedCount} of {totalCount}...", (double)updatedCount / totalCount * 100);

                    if (data.LocationPoint == null)
                    {
                        LECG.Services.Logging.Logger.Instance.LogWarning("[FamilyConversionService] Skipping non-point-based instance during replacement.");
                        continue;
                    }

                    XYZ loc = data.LocationPoint;
                    Level? lev = ResolvePlacementLevel(doc, data.LevelId);
                    FamilyInstance? newInstance = TryPlaceReplacementInstance(doc, newSymbol, loc, lev);

                    if (newInstance == null)
                    {
                        continue;
                    }

                    ApplyCapturedInstanceData(data, newInstance, loc);
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

        private static FamilyInstance? TryPlaceReplacementInstance(Document doc, FamilySymbol newSymbol, XYZ location, Level? level)
        {
            if (level != null)
            {
                try
                {
                    return doc.Create.NewFamilyInstance(location, newSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                }
                catch (ArgumentException ex)
                {
                    LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Level-based placement failed: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Level-based placement failed: {ex.Message}");
                }
                catch (RevitExceptions.InvalidOperationException ex)
                {
                    LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Level-based placement failed: {ex.Message}");
                }
            }

            try
            {
                return doc.Create.NewFamilyInstance(location, newSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            }
            catch (ArgumentException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Point-based placement failed: {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Point-based placement failed: {ex.Message}");
                return null;
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Point-based placement failed: {ex.Message}");
                return null;
            }
        }

        private sealed record CapturedFamilyInstances(
            IReadOnlyList<FamilyInstanceData> CapturedData,
            IReadOnlyCollection<ElementId> InstanceIds);
    }
}
