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
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;

        public FamilyConversionService() : this(new FamilyTemplatePathService(), new FamilyGeometryCollectionService(), new FamilyTempFileCleanupService(), new FamilyLoadOptionsFactory())
        {
        }

        public FamilyConversionService(IFamilyTemplatePathService templatePathService, IFamilyGeometryCollectionService geometryCollectionService, IFamilyTempFileCleanupService tempFileCleanupService, IFamilyLoadOptionsFactory familyLoadOptionsFactory)
        {
            _templatePathService = templatePathService;
            _geometryCollectionService = geometryCollectionService;
            _tempFileCleanupService = tempFileCleanupService;
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
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

                    // Set Family Parameters: Work Plane Based = True
                    Family targetFamilyRoot = targetFamilyDoc.OwnerFamily;
                    
                    Parameter? pWorkPlane = targetFamilyRoot.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
                    if (pWorkPlane != null && !pWorkPlane.IsReadOnly) pWorkPlane.Set(1); // True

                    Parameter? pAlwaysVertical = targetFamilyRoot.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
                    if (pAlwaysVertical != null && !pAlwaysVertical.IsReadOnly) pAlwaysVertical.Set(0); // False

                    if (idsToCopy.Count > 0)
                    {
                        CopyPasteOptions options = new CopyPasteOptions();
                        ElementTransformUtils.CopyElements(sourceFamilyDoc, idsToCopy, targetFamilyDoc, Transform.Identity, options);
                    }

                    tTarget.Commit();
                }

                // 5. Save Target Family
                string tempDir = Path.GetTempPath();
                tempFamilyPath = Path.Combine(tempDir, targetFamilyName + ".rfa");
                
                if (File.Exists(tempFamilyPath)) File.Delete(tempFamilyPath);
                
                SaveAsOptions saveOpts = new SaveAsOptions() { OverwriteExistingFile = true };
                targetFamilyDoc.SaveAs(tempFamilyPath, saveOpts);
                Logger.Instance.Log($"Saved temporary family to: {tempFamilyPath}");

                // 6. Load into Project
                using (Transaction tProject = new Transaction(doc, "Load Converted Family"))
                {
                    tProject.Start();
                    
                    Family? loadedFamily = null;
                    doc.LoadFamily(tempFamilyPath, _familyLoadOptionsFactory.Create(), out loadedFamily);
                    
                    if (loadedFamily != null)
                    {
                        Logger.Instance.Log($"Success! Loaded family: {loadedFamily.Name}");
                        Logger.Instance.UpdateProgress(100, "Done");
                    }
                    else
                    {
                        Logger.Instance.Log("Warning: Family loaded but returned null (already existed?).");
                    }

                    tProject.Commit();
                }
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
