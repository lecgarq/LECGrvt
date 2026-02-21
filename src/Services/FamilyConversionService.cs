using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Models;
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

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilySourceDocumentService(), new FamilyConversionExecutionService(new FamilyTargetDocumentService(), new FamilyGeometryCopyService(new FamilyGeometryCollectionService(), new FamilyParameterSetupService()), new FamilySaveLoadService(new FamilySaveService(), new FamilyProjectLoadService(new FamilyLoadOptionsFactory()))), new FamilyConversionNamingService(), new FamilyConversionLoggingService(), new FamilyConversionFinalizeService(new FamilyTempFileCleanupService()))
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilySourceDocumentService familySourceDocumentService, IFamilyConversionExecutionService familyConversionExecutionService, IFamilyConversionNamingService familyConversionNamingService, IFamilyConversionLoggingService familyConversionLoggingService, IFamilyConversionFinalizeService familyConversionFinalizeService)
        {
            _templatePathService = templatePathService;
            _familySourceDocumentService = familySourceDocumentService;
            _familyConversionExecutionService = familyConversionExecutionService;
            _familyConversionNamingService = familyConversionNamingService;
            _familyConversionLoggingService = familyConversionLoggingService;
            _familyConversionFinalizeService = familyConversionFinalizeService;
        }

        public void ConvertFamily(Document doc, FamilyInstance instance, string customName, string templatePath, bool isTemporary)
        {
            if (instance == null) return;

            Family sourceFamily = instance.Symbol.Family;
            string sourceFamilyName = sourceFamily.Name;
            string targetFamilyName = _familyConversionNamingService.ResolveTargetFamilyName(doc, sourceFamilyName, customName);

            _familyConversionLoggingService.LogStart(sourceFamilyName, targetFamilyName, templatePath, isTemporary);

            if (instance.Host != null)
            {
                _familyConversionLoggingService.LogWarning($"The selected family is hosted on {instance.Host.Name}. Hosting may be lost depending on the target template.");
            }

            Document? sourceFamilyDoc = _familySourceDocumentService.Open(doc, sourceFamily);
            if (sourceFamilyDoc == null)
            {
                return;
            }

            Document? targetFamilyDoc = null;
            string tempFamilyPath = "";

            try
            {
                (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(doc, sourceFamilyDoc, templatePath, targetFamilyName);
                if (targetFamilyDoc == null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _familyConversionLoggingService.LogCriticalError(ex.Message, ex.StackTrace ?? "");
            }
            finally
            {
                _familyConversionFinalizeService.Finalize(sourceFamilyDoc, targetFamilyDoc, tempFamilyPath, isTemporary);
            }
        }

        public void ConvertFamilyBatch(Document doc, IEnumerable<FamilyInstance> instances, string customName, string templatePath, bool isTemporary, bool replaceInPlace)
        {
            if (instances == null || !instances.Any()) return;

            // Group by family to minimize redundant family document conversions
            var instancesByFamily = instances.GroupBy(i => i.Symbol.Family.Id);

            foreach (var group in instancesByFamily)
            {
                var firstInstance = group.First();
                Family sourceFamily = firstInstance.Symbol.Family;
                string sourceFamilyName = sourceFamily.Name;
                string targetFamilyName = _familyConversionNamingService.ResolveTargetFamilyName(doc, sourceFamilyName, customName);

                _familyConversionLoggingService.LogStart(sourceFamilyName, targetFamilyName, templatePath, isTemporary);

                Document? sourceFamilyDoc = _familySourceDocumentService.Open(doc, sourceFamily);
                if (sourceFamilyDoc == null) continue;

                Document? targetFamilyDoc = null;
                string tempFamilyPath = "";

                try
                {
                    (targetFamilyDoc, tempFamilyPath) = _familyConversionExecutionService.Execute(doc, sourceFamilyDoc, templatePath, targetFamilyName);
                    if (targetFamilyDoc == null) continue;

                    // All conversion logic happens here. Now we handle the placement if Replace is true.
                    if (replaceInPlace)
                    {
                        // Get the newly loaded family from the project
                        Family? newFamily = new FilteredElementCollector(doc)
                            .OfClass(typeof(Family))
                            .Cast<Family>()
                            .FirstOrDefault(f => f.Name == targetFamilyName);

                        if (newFamily != null)
                        {
                            FamilySymbol? newSymbol = doc.GetElement(newFamily.GetFamilySymbolIds().First()) as FamilySymbol;
                            if (newSymbol != null)
                            {
                                if (!newSymbol.IsActive) newSymbol.Activate();

                                foreach (var oldInstance in group)
                                {
                                    // Capture
                                    var data = FamilyInstanceData.Capture(oldInstance);

                                    // Place
                                    FamilyInstance newInstance = doc.Create.NewFamilyInstance(
                                        data.LocationPoint,
                                        newSymbol,
                                        oldInstance.StructuralType);

                                    // Restore
                                    data.Apply(newInstance);

                                    // Delete old
                                    doc.Delete(oldInstance.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _familyConversionLoggingService.LogCriticalError(ex.Message, ex.StackTrace ?? "");
                }
                finally
                {
                    _familyConversionFinalizeService.Finalize(sourceFamilyDoc, targetFamilyDoc, tempFamilyPath, isTemporary);
                }
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }
    }
}
