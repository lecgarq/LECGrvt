using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FamilyEditorService : IFamilyEditorService
    {
        private readonly IFamilyLoadOptionsFactory _loadOptionsFactory;
        private readonly ITransactionService _transactionService;

        public FamilyEditorService(
            IFamilyLoadOptionsFactory loadOptionsFactory,
            ITransactionService transactionService)
        {
            _loadOptionsFactory = loadOptionsFactory;
            _transactionService = transactionService;
        }

        public bool ChangeCategory(Autodesk.Revit.DB.Family family, Autodesk.Revit.DB.Category newCategory)
        {
            if (family == null) return false;

            // PRE-CHECK: Determine Source Type (2D vs 3D) BEFORE entering ProcessFamily
            // This ensures the exception reaches the caller (Command) instead of being swallowed by ProcessFamily's log-and-return-false.
            bool isSourceModel = IsModelCategory(family.FamilyCategory);
            bool isTargetModel = IsModelCategory(newCategory);

            // Logic: 2D to 3D jump is physically impossible in Revit API
            if (isSourceModel != isTargetModel)
            {
                string msg = isSourceModel ? "3D (Model)" : "2D (Detail/Annotation)";
                string targetMsg = isTargetModel ? "3D (Model)" : "2D (Detail/Annotation)";
                throw new InvalidOperationException($"Revit Platform Limit: Cannot convert {msg} family to {targetMsg} category. These use different family templates.");
            }

            return ProcessFamily(family, (familyDoc) =>
            {
                if (familyDoc.OwnerFamily != null)
                {
                    // Try getting category by BuiltInCategory (most reliable)
                    BuiltInCategory bic = (BuiltInCategory)newCategory.Id.Value;
                    Category? docCat = familyDoc.Settings.Categories.get_Item(bic);

                    // Fallback to name matching
                    if (docCat == null)
                        docCat = familyDoc.Settings.Categories.get_Item(newCategory.Name);

                    if (docCat == null)
                        throw new InvalidOperationException($"Category '{newCategory.Name}' is not loaded or valid in this family document.");

                    try
                    {
                        familyDoc.OwnerFamily.FamilyCategory = docCat;
                    }
                    catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
                    {
                        // WORKAROUND: Try the "Generic Model Bridge" if it's a model family
                        if (isSourceModel)
                        {
                            try
                            {
                                Category gmCat = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);
                                familyDoc.OwnerFamily.FamilyCategory = gmCat;
                                familyDoc.OwnerFamily.FamilyCategory = docCat; // Try again after resetting to GM
                            }
                            catch (Exception bridgeEx) when (IsExpectedFamilyEditorException(bridgeEx))
                            {
                                throw new InvalidOperationException($"Revit refused category change: {ex.Message}");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Revit refused category change: {ex.Message}");
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("This family document has no OwnerFamily (cannot change category).");
                }
            });
        }

        private bool IsModelCategory(Category? cat)
        {
            if (cat == null) return true;

            // Known 2D Categories that are NOT model categories
            BuiltInCategory bic = (BuiltInCategory)cat.Id.Value;

            return bic switch
            {
                BuiltInCategory.OST_DetailComponents => false,
                BuiltInCategory.OST_GenericAnnotation => false,
                BuiltInCategory.OST_Tags => false,
                BuiltInCategory.OST_TextNotes => false,
                BuiltInCategory.OST_Dimensions => false,
                BuiltInCategory.OST_TitleBlocks => false,
                _ => true // Assume 3D/Model if not in the 2D list
            };
        }

        public bool ProcessFamily(Autodesk.Revit.DB.Family family, Action<Autodesk.Revit.DB.Document> action)
        {
            if (family == null) return false;
            ArgumentNullException.ThrowIfNull(action);

            Autodesk.Revit.DB.Document projectDoc = family.Document;
            Autodesk.Revit.DB.Document familyDoc = null!;

            try
            {
                familyDoc = projectDoc.EditFamily(family);
                if (familyDoc == null)
                {
                    LECG.Services.Logging.Logger.Instance.Log($"  [ERROR] Could not enter Family Editor for '{family.Name}'.");
                    return false;
                }

                _transactionService.Run(familyDoc, "Silent Family Edit", _ =>
                {
                    action(familyDoc);
                });

                // Load back into project (LoadFamily manages its own transaction internally)
                var options = _loadOptionsFactory.Create();
                familyDoc.LoadFamily(projectDoc, options);

                return true;
            }
            catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
            {
                LECG.Services.Logging.Logger.Instance.Log($"  [ERROR] {ex.Message}");
                return false;
            }
            finally
            {
                TryCloseFamilyDocument(familyDoc);
            }
        }

        public Family RecreateAs(Family sourceFamily, Category targetCategory)
        {
            if (sourceFamily == null) throw new ArgumentNullException(nameof(sourceFamily));

            Document projectDoc = sourceFamily.Document;
            Application app = projectDoc.Application;

            // 1. Determine the correct template path with a robust search
            string templatePath = FindTemplate(targetCategory, app.VersionNumber);

            if (string.IsNullOrEmpty(templatePath))
            {
                string baseDir = Configuration.RevitConstants.GetFamilyTemplatesBaseDir(app.VersionNumber);
                throw new Exception($"Creator Engine Error: Could not find suitable .rft template in {baseDir}. Please ensure English or Spanish templates are installed.");
            }

            Document sourceDoc = projectDoc.EditFamily(sourceFamily);
            Document targetDoc = null!;

            try
            {
                // 2. Create the new family document
                targetDoc = app.NewFamilyDocument(templatePath);

                // 3. Nest source family into target
                string safeName = sourceFamily.Name.Replace(" ", "_").Replace("[", "").Replace("]", "");
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), safeName + "_nest.rfa");

                try
                {
                    // Save source family to disk so it can be loaded
                    SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                    sourceDoc.SaveAs(tempPath, saveOptions);
                    sourceDoc.Close(false); // Close so we don't have locking issues

                    _transactionService.Run(targetDoc, "Nest Source Family", _ =>
                    {
                        // Load the Detail Item into the new 3D family
                        Family nestedFamily;
                        bool loadSuccess = targetDoc.LoadFamily(tempPath, _loadOptionsFactory.Create(), out nestedFamily);

                        if (loadSuccess && nestedFamily != null)
                        {
                            // Place the symbol at the origin (Ref Level)
                            FamilySymbol? symbol = new FilteredElementCollector(targetDoc)
                                .OfClass(typeof(FamilySymbol))
                                .Cast<FamilySymbol>()
                                .FirstOrDefault(s => s.Family.Id == nestedFamily.Id);

                            if (symbol != null)
                            {
                                if (!symbol.IsActive) symbol.Activate();
                                targetDoc.FamilyCreate.NewFamilyInstance(XYZ.Zero, symbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                        }
                    });
                }
                catch (Exception nestEx) when (IsExpectedFamilyNestingException(nestEx))
                {
                    throw new Exception($"Nesting Failed: {nestEx.Message}. Harvesting logic exhausted.");
                }
                finally
                {
                    TryDeleteTemporaryFile(tempPath);
                }

                // 4. Set the Category in the new family
                _transactionService.Run(targetDoc, "Set Category", _ =>
                {
                    BuiltInCategory bic = (BuiltInCategory)targetCategory.Id.Value;
                    Category docCat = targetDoc.Settings.Categories.get_Item(bic);
                    if (docCat != null && targetDoc.OwnerFamily != null)
                    {
                        targetDoc.OwnerFamily.FamilyCategory = docCat;
                    }
                });

                // 5. Load into project with new name
                string suffix = "-TRANSPLANTED";
                string newName = sourceFamily.Name + suffix;
                Family newFamily = targetDoc.LoadFamily(projectDoc, _loadOptionsFactory.Create());

                // Rename in project context
                _transactionService.Run(projectDoc, "Rename Transplanted Family", _ =>
                {
                    newFamily.Name = newName;
                });

                return newFamily;
            }
            finally
            {
                // Note: sourceDoc was closed earlier in the try block to avoid lock
                TryCloseFamilyDocument(targetDoc);
            }
        }

        public void BatchProcess(IEnumerable<Autodesk.Revit.DB.Family> families, Action<Autodesk.Revit.DB.Document> action)
        {
            ArgumentNullException.ThrowIfNull(families);
            ArgumentNullException.ThrowIfNull(action);

            // Future performance enhancement: consider batching transactions if Revit allows
            // For now, iterate with individual family document management for memory safety
            foreach (var family in families)
            {
                ProcessFamily(family, action);
            }
        }

        private static bool IsExpectedFamilyEditorException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static bool IsExpectedFamilyNestingException(Exception ex)
        {
            return IsExpectedFamilyEditorException(ex)
                || ex is System.IO.IOException
                || ex is UnauthorizedAccessException;
        }

        private static void TryCloseFamilyDocument(Autodesk.Revit.DB.Document? familyDoc)
        {
            if (familyDoc == null || !familyDoc.IsValidObject)
            {
                return;
            }

            try
            {
                familyDoc.Close(false);
            }
            catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
            {
                LECG.Services.Logging.Logger.Instance.LogWarning($"[FamilyEditorService] Could not close family document: {ex.Message}");
            }
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(path);
            }
            catch (System.IO.IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string FindTemplate(Category targetCategory, string versionNumber)
        {
            bool isModel = IsModelCategory(targetCategory);

            string baseDir = Configuration.RevitConstants.GetFamilyTemplatesBaseDir(versionNumber);
            string[] searchDirs = new[]
            {
                System.IO.Path.Combine(baseDir, "English"),
                System.IO.Path.Combine(baseDir, "Spanish"),
                System.IO.Path.Combine(baseDir, "English-Imperial")
            };

            // Priority list of file names (Metric then Imperial, localized common names)
            string[] modelFiles = new[] { "Metric Generic Model.rft", "Generic Model.rft", "Modelo genérico métrico.rft", "Modelo genérico.rft" };
            string[] detailFiles = new[] { "Metric Detail Item.rft", "Detail Item.rft", "Elemento de detalle métrico.rft", "Elemento de detalle.rft" };

            string[] targetFiles = isModel ? modelFiles : detailFiles;

            foreach (string dir in searchDirs)
            {
                if (!System.IO.Directory.Exists(dir)) continue;

                foreach (string file in targetFiles)
                {
                    string path = System.IO.Path.Combine(dir, file);
                    if (System.IO.File.Exists(path)) return path;
                }
            }

            return string.Empty;
        }
    }
}
