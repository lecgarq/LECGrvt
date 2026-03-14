using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            if (instances == null || !instances.Any()) return;

            int totalCount = instances.Count();
            int currentCount = 0;

            using (new ExecutionTimer($"Batch Conversion: {totalCount} instances"))
            {
                var instancesByFamily = instances.GroupBy(i => i.Symbol.Family.Id);

                foreach (var group in instancesByFamily)
                {
                    var firstInstance = group.First();
                    Family sourceFamily = firstInstance.Symbol.Family;
                    string sourceFamilyName = sourceFamily.Name;
                    
                    // Force the name to matching exactly to overwrite existing family definition
                    string targetFamilyName = sourceFamilyName;

                    string resolvedTemplatePath = templatePath;
                    if (string.IsNullOrEmpty(resolvedTemplatePath))
                    {
                        resolvedTemplatePath = @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\LECG_070_GENERIC-MODELS.rft";
                    }

                    using (new ExecutionTimer($"Family Group: {sourceFamilyName}"))
                    {
                        reporter?.Report($"Converting Family: {sourceFamilyName}...", (double)currentCount / totalCount * 100);
                        _familyConversionLoggingService.LogStart(sourceFamilyName, targetFamilyName, resolvedTemplatePath, isTemporary: false);

                        // 1. CAPTURE ALL INSTANCES OF THIS FAMILY IN THE ENTIRE PROJECT
                        var allInstancesOfFamily = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(i => i.Symbol != null && i.Symbol.Family.Id == sourceFamily.Id)
                            .ToList();

                        var capturedDataList = allInstancesOfFamily.Select(FamilyInstanceData.Capture).ToList();
                        var oldInstanceIds = allInstancesOfFamily.Select(i => i.Id).ToList();
                        
                        // 2. OPEN SOURCE DOCUMENT
                        Document? sourceFamilyDoc = _familySourceDocumentService.Open(doc, sourceFamily);
                        if (sourceFamilyDoc == null) { currentCount += group.Count(); continue; }

                        Document? targetFamilyDoc = null;
                        string tempFamilyPath = "";

                        try
                        {
                            // 3. DELETE OLD INSTANCES BEFORE EXECUTION
                            if (replaceInPlace)
                            {
                                _transactionService.Run(doc, "Delete Old Instances for Conversion", _ =>
                                {
                                    foreach (var id in oldInstanceIds) { try { doc.Delete(id); } catch (Exception ex) { LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Failed to delete instance {id}: {ex.Message}"); } }
                                });
                                LECG.Services.Logging.Logger.Instance.Log($"Cleared {oldInstanceIds.Count} instances from project to unlock native conversion.");
                            }

                            // 4. EXECUTE CONVERSION
                            (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(doc, sourceFamily, sourceFamilyDoc, resolvedTemplatePath, targetFamilyName);
                            
                            if (targetFamilyDoc != null && replaceInPlace)
                            {
                                // 5. FIND NEW SYMBOL
                                Family? newFamily = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f => f.Name == targetFamilyName);
                                if (newFamily != null)
                                {
                                    var symbolIds = newFamily.GetFamilySymbolIds();
                                    FamilySymbol? newSymbol = symbolIds.Any() ? doc.GetElement(symbolIds.First()) as FamilySymbol : null;

                                    if (newSymbol != null)
                                    {
                                        // 6. PLACE NEW UNHOSTED INSTANCES
                                        _transactionService.RunWithOptions(doc, "Replace Instances", _ =>
                                        {
                                            if (!newSymbol.IsActive) newSymbol.Activate();

                                            foreach (var data in capturedDataList)
                                            {
                                                currentCount++;
                                                reporter?.Report($"Placing Instance {currentCount} of {totalCount}...", (double)currentCount / totalCount * 100);
                                                
                                                try {
                                                    XYZ loc = data.LocationPoint ?? XYZ.Zero;
                                                    Level lev = doc.GetElement(data.LevelId) as Level ?? doc.ActiveView?.GenLevel ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
                                                    
                                                    FamilyInstance ni = null;
                                                    // Strategy A: Standard Level-based placement (Revit handles Work Plane auto-association for unhosted families)
                                                    try {
                                                        ni = doc.Create.NewFamilyInstance(loc, newSymbol, lev, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                                    } 
                                                    catch {
                                                    // Strategy B: Pure Point-based fallback
                                                        try { ni = doc.Create.NewFamilyInstance(loc, newSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural); } 
                                                        catch (Exception ex2) { LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyConversionService] Placement Strategy B failed: {ex2.Message}"); }
                                                    }

                                                    if (ni != null) 
                                                    { 
                                                        data.Apply(ni); 
                                                        LECG.Services.Logging.Logger.Instance.Log($"  ✓ Instance placed at ({loc.X:F2}, {loc.Y:F2}, {loc.Z:F2})");
                                                    }
                                                } catch (Exception ex) { LECG.Services.Logging.Logger.Instance.Log($"  Placement error: {ex.Message}"); }
                                            }
                                        }, options => options.SetForcedModalHandling(false));
                                    }
                                }
                            }
                            else if (targetFamilyDoc == null)
                            {
                                LECG.Services.Logging.Logger.Instance.Log("ERROR: targetFamilyDoc was null. Conversion failed internally.");
                                currentCount += group.Count();
                            }
                        }
                        catch (Exception ex)
                        {
                            _familyConversionLoggingService.LogCriticalError(ex.Message, ex.StackTrace ?? "");
                            currentCount += group.Count();
                        }
                        finally
                        {
                            _familyConversionFinalizeService.Finalize(sourceFamilyDoc, targetFamilyDoc, tempFamilyPath, isTemporary: false);
                        }
                    }
                }
                reporter?.Report("Batch Conversion Complete.", 100);
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }
    }
}
