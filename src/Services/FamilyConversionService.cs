using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LECG.Services
{
    public class FamilyConversionService : IFamilyConversionService
    {
        private readonly IFamilyTemplatePathService _templatePathService;
        private readonly IFamilyGeometryCollectionService _geometryCollectionService;
        private readonly IFamilyTempFileCleanupService _tempFileCleanupService;
        private readonly IFamilyProjectLoadService _familyProjectLoadService;
        private readonly IFamilyParameterSetupService _familyParameterSetupService;
        private readonly IFamilySaveService _familySaveService;

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilyGeometryCollectionService(), new FamilyTempFileCleanupService(), new FamilyProjectLoadService(new FamilyLoadOptionsFactory()), new FamilyParameterSetupService(), new FamilySaveService())
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilyGeometryCollectionService geometryCollectionService, IFamilyTempFileCleanupService tempFileCleanupService, IFamilyProjectLoadService familyProjectLoadService, IFamilyParameterSetupService familyParameterSetupService, IFamilySaveService familySaveService)
        {
            _templatePathService = templatePathService;
            _geometryCollectionService = geometryCollectionService;
            _tempFileCleanupService = tempFileCleanupService;
            _familyProjectLoadService = familyProjectLoadService;
            _familyParameterSetupService = familyParameterSetupService;
            _familySaveService = familySaveService;
        }

        public void ConvertFamily(Document doc, FamilyInstance instance, string customName, string templatePath, bool isTemporary)
        {
            if (instance == null) return;

            Family sourceFamily = instance.Symbol.Family;
            string sourceFamilyName = sourceFamily.Name;
            string targetFamilyName = string.IsNullOrWhiteSpace(customName) ? $"{sourceFamilyName}_Converted" : customName;

            Logger.Instance.Log($"Converting Family: {sourceFamilyName} -> {targetFamilyName}");
            Logger.Instance.Log($"Template: {Path.GetFileName(templatePath)}");
            Logger.Instance.Log($"Temporary Mode: {isTemporary}");

            // 1. Open Source Family Document
            Document sourceFamilyDoc = doc.EditFamily(sourceFamily);
            if (sourceFamilyDoc == null)
            {
                Logger.Instance.Log("Error: Could not open source family for editing.");
                return;
            }

            Document? targetFamilyDoc = null;
            string tempFamilyPath = "";

            try
            {
                // 2. Create Target Family Document
                if (!File.Exists(templatePath))
                {
                    Logger.Instance.Log($"Error: Template not found at {templatePath}");
                    return;
                }
                
                targetFamilyDoc = doc.Application.NewFamilyDocument(templatePath);
                
                if (targetFamilyDoc == null)
                {
                    Logger.Instance.Log("Error: Could not create new family document.");
                    return;
                }

                // 3. Collect Geometry from Source Family
                List<ElementId> idsToCopy = _geometryCollectionService.CollectGeometryElementIds(sourceFamilyDoc);

                Logger.Instance.Log($"Found {idsToCopy.Count} geometry elements to copy.");

                // 4. Copy Geometry to Target Family
                using (Transaction tTarget = new Transaction(targetFamilyDoc, "Copy Geometry"))
                {
                    tTarget.Start();

                    _familyParameterSetupService.ConfigureTargetFamilyParameters(targetFamilyDoc);

                    if (idsToCopy.Count > 0)
                    {
                        CopyPasteOptions options = new CopyPasteOptions();
                        ElementTransformUtils.CopyElements(sourceFamilyDoc, idsToCopy, targetFamilyDoc, Transform.Identity, options);
                    }

                    tTarget.Commit();
                }

                tempFamilyPath = _familySaveService.SaveTemp(targetFamilyDoc, targetFamilyName);

                _familyProjectLoadService.Load(doc, tempFamilyPath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Critical Error: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace ?? "");
            }
            finally
            {
                sourceFamilyDoc.Close(false);
                if (targetFamilyDoc != null) targetFamilyDoc.Close(false);
                _tempFileCleanupService.Cleanup(tempFamilyPath, isTemporary);
            }
        }

        public string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category)
        {
            return _templatePathService.GetTargetTemplatePath(app, category);
        }

    }
}
