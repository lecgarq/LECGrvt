using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FamilyEditorService
    {
        private readonly FamilyLoadOptionsFactory _loadOptionsFactory;
        private readonly ITransactionService _transactionService;
        private readonly ILogger _logger;

        public FamilyEditorService(
            FamilyLoadOptionsFactory loadOptionsFactory,
            ITransactionService transactionService,
            ILogger logger)
        {
            _loadOptionsFactory = loadOptionsFactory;
            _transactionService = transactionService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        private bool ProcessFamily(Autodesk.Revit.DB.Family family, Action<Autodesk.Revit.DB.Document> action)
        {
            if (family == null) return false;
            ArgumentNullException.ThrowIfNull(action);

            Autodesk.Revit.DB.Document projectDoc = family.Document;
            Autodesk.Revit.DB.Document familyDoc = null!;

            // Capture the name FIRST: LoadFamily invalidates the original Family handle
            // (a category change recreates the family), so touching `family` after the
            // reload throws InvalidObjectException.
            string familyName = family.Name;

            try
            {
                // A category change rebuilds the family's parameter set, so Revit re-creates
                // parameters on reload and resets their values even with
                // overwriteParameterValues = false. Snapshot every instance's and type's
                // writable values BEFORE the edit and restore them AFTER the reload.
                FamilyValueSnapshot snapshot = SnapshotFamilyParameterValues(projectDoc, family);

                familyDoc = projectDoc.EditFamily(family);
                if (familyDoc == null)
                {
                    _logger.LogError($"Could not enter Family Editor for '{familyName}'.", scope: "FamilyEditor");
                    return false;
                }

                _transactionService.Run(familyDoc, "Silent Family Edit", _ =>
                {
                    action(familyDoc);
                });

                // Load back into project (LoadFamily manages its own transaction internally).
                var options = _loadOptionsFactory.Create(overwriteParameterValues: false);
                Family? reloadedFamily = familyDoc.LoadFamily(projectDoc, options);

                RestoreFamilyParameterValues(projectDoc, snapshot, reloadedFamily, familyName);

                return true;
            }
            catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
            {
                _logger.LogError(ex.Message, scope: "FamilyEditor");
                return false;
            }
            finally
            {
                TryCloseFamilyDocument(familyDoc);
            }
        }

        /// <summary>
        /// Captures every writable parameter value on the family's placed instances and types.
        /// Instances keep their element ids across a reload; family types (symbols) may be
        /// recreated with NEW ids when the category changes, so they are keyed by name.
        /// </summary>
        private FamilyValueSnapshot SnapshotFamilyParameterValues(Document projectDoc, Family family)
        {
            var instanceValues = new Dictionary<ElementId, List<ParameterSnapshot>>();
            var typeValues = new Dictionary<string, List<ParameterSnapshot>>(StringComparer.Ordinal);

            foreach (ElementId symbolId in family.GetFamilySymbolIds())
            {
                if (projectDoc.GetElement(symbolId) is FamilySymbol symbol)
                {
                    typeValues[symbol.Name] = SnapshotElementParameters(symbol);
                }
            }

            foreach (FamilyInstance instance in new FilteredElementCollector(projectDoc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol?.Family?.Id == family.Id))
            {
                instanceValues[instance.Id] = SnapshotElementParameters(instance);
            }

            return new FamilyValueSnapshot(instanceValues, typeValues);
        }

        private List<ParameterSnapshot> SnapshotElementParameters(Element element)
        {
            var values = new List<ParameterSnapshot>();
            foreach (Parameter parameter in element.Parameters)
            {
                try
                {
                    if (parameter.IsReadOnly || !parameter.HasValue) continue;

                    values.Add(parameter.StorageType switch
                    {
                        StorageType.Double => new ParameterSnapshot(parameter.Definition.Name, StorageType.Double, parameter.AsDouble(), null, null, null),
                        StorageType.Integer => new ParameterSnapshot(parameter.Definition.Name, StorageType.Integer, null, parameter.AsInteger(), null, null),
                        StorageType.String => new ParameterSnapshot(parameter.Definition.Name, StorageType.String, null, null, parameter.AsString(), null),
                        StorageType.ElementId => new ParameterSnapshot(parameter.Definition.Name, StorageType.ElementId, null, null, null, parameter.AsElementId()),
                        _ => null!
                    });
                }
                catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
                {
                    _logger.LogWarning($"Could not snapshot parameter on element {element.Id}: {ex.Message}", scope: "FamilyEditor");
                }
            }

            values.RemoveAll(snapshot => snapshot == null);
            return values;
        }

        private void RestoreFamilyParameterValues(
            Document projectDoc,
            FamilyValueSnapshot snapshot,
            Family? reloadedFamily,
            string familyName)
        {
            if (snapshot.InstanceValues.Count == 0 && snapshot.TypeValues.Count == 0) return;

            // Resolve the post-reload symbols by NAME (their ids may have changed).
            var symbolsByName = new Dictionary<string, FamilySymbol>(StringComparer.Ordinal);
            Family? currentFamily = reloadedFamily ?? FindFamilyByName(projectDoc, familyName);
            if (currentFamily != null && currentFamily.IsValidObject)
            {
                foreach (ElementId symbolId in currentFamily.GetFamilySymbolIds())
                {
                    if (projectDoc.GetElement(symbolId) is FamilySymbol symbol)
                    {
                        symbolsByName[symbol.Name] = symbol;
                    }
                }
            }

            int restored = 0;
            _transactionService.Run(projectDoc, "Restore Parameter Values", _ =>
            {
                foreach (var (typeName, parameterSnapshots) in snapshot.TypeValues)
                {
                    if (!symbolsByName.TryGetValue(typeName, out FamilySymbol? symbol)) continue;

                    foreach (ParameterSnapshot parameterSnapshot in parameterSnapshots)
                    {
                        if (TryRestoreParameter(projectDoc, symbol, parameterSnapshot)) restored++;
                    }
                }

                foreach (var (elementId, parameterSnapshots) in snapshot.InstanceValues)
                {
                    Element? element = projectDoc.GetElement(elementId);
                    if (element == null || !element.IsValidObject) continue;

                    foreach (ParameterSnapshot parameterSnapshot in parameterSnapshots)
                    {
                        if (TryRestoreParameter(projectDoc, element, parameterSnapshot)) restored++;
                    }
                }
            });

            _logger.Log($"Restored {restored} parameter value(s) on '{familyName}' after reload.", scope: "FamilyEditor");
        }

        private static Family? FindFamilyByName(Document projectDoc, string familyName)
        {
            return new FilteredElementCollector(projectDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.Ordinal));
        }

        private bool TryRestoreParameter(Document projectDoc, Element element, ParameterSnapshot snapshot)
        {
            try
            {
                Parameter? parameter = element.LookupParameter(snapshot.Name);
                if (parameter == null || parameter.IsReadOnly || parameter.StorageType != snapshot.Storage) return false;

                switch (snapshot.Storage)
                {
                    case StorageType.Double when snapshot.DoubleValue is double d:
                        if (parameter.HasValue && Math.Abs(parameter.AsDouble() - d) < 1e-9) return false;
                        return parameter.Set(d);
                    case StorageType.Integer when snapshot.IntValue is int i:
                        if (parameter.HasValue && parameter.AsInteger() == i) return false;
                        return parameter.Set(i);
                    case StorageType.String when snapshot.StringValue != null:
                        if (parameter.HasValue && string.Equals(parameter.AsString(), snapshot.StringValue, StringComparison.Ordinal)) return false;
                        return parameter.Set(snapshot.StringValue);
                    case StorageType.ElementId when snapshot.IdValue is ElementId id:
                        if (parameter.HasValue && parameter.AsElementId() == id) return false;
                        // Don't point a parameter at an element that no longer exists.
                        if (id.Value > 0 && projectDoc.GetElement(id) == null) return false;
                        return parameter.Set(id);
                    default:
                        return false;
                }
            }
            catch (Exception ex) when (IsExpectedFamilyEditorException(ex))
            {
                _logger.LogWarning($"Could not restore parameter '{snapshot.Name}' on element {element.Id}: {ex.Message}", scope: "FamilyEditor");
                return false;
            }
        }

        private sealed record ParameterSnapshot(
            string Name,
            StorageType Storage,
            double? DoubleValue,
            int? IntValue,
            string? StringValue,
            ElementId? IdValue);

        private sealed record FamilyValueSnapshot(
            Dictionary<ElementId, List<ParameterSnapshot>> InstanceValues,
            Dictionary<string, List<ParameterSnapshot>> TypeValues);

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
                throw new InvalidOperationException($"Creator Engine Error: Could not find suitable .rft template in {baseDir}. Please ensure English or Spanish templates are installed.");
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
                    throw new InvalidOperationException($"Nesting Failed: {nestEx.Message}. Harvesting logic exhausted.");
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

        private static bool IsExpectedFamilyEditorException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException
                || ex is RevitExceptions.InvalidObjectException;
        }

        private static bool IsExpectedFamilyNestingException(Exception ex)
        {
            return IsExpectedFamilyEditorException(ex)
                || ex is System.IO.IOException
                || ex is UnauthorizedAccessException;
        }

        private void TryCloseFamilyDocument(Autodesk.Revit.DB.Document? familyDoc)
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
                _logger.LogWarning($"Could not close family document: {ex.Message}", scope: "FamilyEditor");
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
