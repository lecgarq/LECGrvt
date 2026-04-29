using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class LinkedModelExportService : ILinkedModelExportService
    {
        private readonly ITransactionService _transactionService;

        public LinkedModelExportService(ITransactionService transactionService)
        {
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        }

        public Dictionary<string, List<ElementId>> GroupElementsByType(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            var groups = new Dictionary<string, List<ElementId>>();

            CollectInstancesByTypeClass<Floor, FloorType>(doc, groups);
            CollectInstancesByTypeClass<Wall, WallType>(doc, groups);
            CollectInstancesByTypeClass<RoofBase, RoofType>(doc, groups);
            CollectInstancesByTypeClass<Ceiling, CeilingType>(doc, groups);
            CollectToposolids(doc, groups);

            return groups;
        }

        public void ExportAndLink(Document doc, Dictionary<string, List<ElementId>> selectedGroups, string outputFolder, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(selectedGroups);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
            ArgumentNullException.ThrowIfNull(reporter);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var exportedGroups = new List<ExportedGroupResult>();
            var allExportedIds = new List<ElementId>();
            int totalGroups = selectedGroups.Count;
            int processed = 0;

            foreach ((string typeName, List<ElementId> elementIds) in selectedGroups)
            {
                processed++;
                double percent = (double)processed / totalGroups * 70;

                if (elementIds.Count == 0)
                {
                    reporter.Log($"Skipped '{typeName}': no elements.");
                    continue;
                }

                List<ElementId> exportableIds = GetExportableElementIds(doc, elementIds);
                if (exportableIds.Count == 0)
                {
                    reporter.Log($"Skipped '{typeName}': no exportable geometry.");
                    continue;
                }

                reporter.Report($"Exporting '{typeName}' ({exportableIds.Count} elements)...", percent);

                string safeFileName = SanitizeFileName(typeName);
                string filePath = Path.Combine(outputFolder, $"{safeFileName}.rvt");

                try
                {
                    ExportGroupToFile(doc, exportableIds, filePath);

                    exportedGroups.Add(new ExportedGroupResult(typeName, filePath, exportableIds));
                    allExportedIds.AddRange(exportableIds);

                    reporter.Log($"Exported '{typeName}' -> {filePath} | copied {exportableIds.Count} elements");
                }
                catch (Exception ex) when (IsExpectedLinkedModelExportException(ex))
                {
                    reporter.LogError($"Failed to export '{typeName}': {ex.Message}");
                }
            }

            if (exportedGroups.Count == 0)
            {
                reporter.LogWarning("No files were exported. Nothing to link.");
                return;
            }

            reporter.Report("Linking exported files and cleaning host model...", 80);

            _transactionService.Run(doc, "Link Exported Models", currentDoc =>
            {
                int linked = 0;

                foreach (ExportedGroupResult group in exportedGroups)
                {
                    try
                    {
                        string linkStatus = LinkExportedFile(currentDoc, group.FilePath);
                        linked++;

                        reporter.Log(
                            $"Linked '{group.TypeName}' -> {group.FilePath} | copied {group.ElementCount} elements | status: {linkStatus}");
                    }
                    catch (Exception ex) when (IsExpectedLinkedModelExportException(ex))
                    {
                        reporter.LogError($"Failed to link '{group.TypeName}' from '{group.FilePath}': {ex.Message}");
                    }
                }

                if (allExportedIds.Count > 0)
                {
                    reporter.Report("Deleting exported elements from host...", 90);

                    try
                    {
                        currentDoc.Delete(allExportedIds);
                        reporter.Log($"Deleted {allExportedIds.Count} exported elements from host model.");
                    }
                    catch (Exception ex) when (IsExpectedLinkedModelExportException(ex))
                    {
                        reporter.LogError($"Failed to delete some elements from host: {ex.Message}");
                    }
                }

                reporter.Log($"Linked {linked} of {exportedGroups.Count} files.");
            });

            reporter.Report("Complete.", 100);
            reporter.Log($"Export complete. {exportedGroups.Count} files created, {allExportedIds.Count} elements moved.");
        }

        private static List<ElementId> GetExportableElementIds(Document doc, IEnumerable<ElementId> elementIds)
        {
            var exportableIds = new List<ElementId>();
            var options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            foreach (ElementId elementId in elementIds)
            {
                Element? element = doc.GetElement(elementId);
                if (element == null)
                {
                    continue;
                }

                if (HasExportableGeometry(element, options))
                {
                    exportableIds.Add(elementId);
                }
            }

            return exportableIds;
        }

        private void ExportGroupToFile(Document sourceDoc, List<ElementId> elementIds, string filePath)
        {
            Document newDoc = sourceDoc.Application.NewProjectDocument(UnitSystem.Metric);

            try
            {
                ApplyCoordinateContext(sourceDoc, newDoc);

                using (Transaction tx = new Transaction(newDoc, "Copy Elements"))
                {
                    tx.Start();

                    try
                    {
                        var copyOptions = new CopyPasteOptions();
                        ElementTransformUtils.CopyElements(sourceDoc, elementIds, newDoc, Transform.Identity, copyOptions);
                        tx.Commit();
                    }
                    catch
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                        {
                            tx.RollBack();
                        }

                        throw;
                    }
                }

                var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                newDoc.SaveAs(filePath, saveOptions);
            }
            finally
            {
                newDoc.Close(false);
            }
        }

        private void ApplyCoordinateContext(Document sourceDoc, Document targetDoc)
        {
            using Transaction tx = new Transaction(targetDoc, "Copy Coordinate Context");
            tx.Start();

            try
            {
                ProjectPosition hostPosition = sourceDoc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                var copiedPosition = new ProjectPosition(
                    hostPosition.EastWest,
                    hostPosition.NorthSouth,
                    hostPosition.Elevation,
                    hostPosition.Angle);

                targetDoc.ActiveProjectLocation.SetProjectPosition(XYZ.Zero, copiedPosition);

                string geoDefinition = sourceDoc.SiteLocation.GeoCoordinateSystemDefinition;
                if (!string.IsNullOrWhiteSpace(geoDefinition))
                {
                    targetDoc.SiteLocation.SetGeoCoordinateSystem(geoDefinition);
                }

                tx.Commit();
            }
            catch
            {
                if (tx.GetStatus() == TransactionStatus.Started)
                {
                    tx.RollBack();
                }

                throw;
            }
        }

        private static string LinkExportedFile(Document doc, string filePath)
        {
            var linkPath = new FilePath(filePath);
            var linkOptions = new RevitLinkOptions(false);
            LinkLoadResult linkResult = RevitLinkType.Create(doc, linkPath, linkOptions);
            ElementId linkTypeId = linkResult.ElementId;

            try
            {
                RevitLinkInstance.Create(doc, linkTypeId, ImportPlacement.Shared);
                return "linked using shared coordinates";
            }
            catch (Exception ex) when (IsExpectedLinkedModelExportException(ex))
            {
                RevitLinkInstance.Create(doc, linkTypeId, ImportPlacement.Origin);
                return "linked at origin fallback";
            }
        }

        private static bool HasExportableGeometry(Element element, Options options)
        {
            GeometryElement? geometry = element.get_Geometry(options);
            if (geometry == null)
            {
                return false;
            }

            return ContainsExportableGeometry(geometry);
        }

        private static bool ContainsExportableGeometry(GeometryElement geometry)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Faces.Size > 0 && solid.Edges.Size > 0)
                {
                    return true;
                }

                if (obj is Mesh mesh && mesh.NumTriangles > 0)
                {
                    return true;
                }

                if (obj is Curve)
                {
                    return true;
                }

                if (obj is GeometryInstance instance && ContainsExportableGeometry(instance.GetInstanceGeometry()))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectInstancesByTypeClass<TInstance, TType>(Document doc, Dictionary<string, List<ElementId>> groups)
            where TInstance : Element
            where TType : ElementType
        {
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(TInstance))
                .WhereElementIsNotElementType()
                .ToList();

            foreach (Element element in instances)
            {
                ElementId typeId = element.GetTypeId();
                if (typeId == null || typeId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Element? typeElement = doc.GetElement(typeId);
                if (typeElement == null)
                {
                    continue;
                }

                string typeName = $"{typeElement.Category?.Name ?? "Unknown"} - {typeElement.Name}";

                if (!groups.ContainsKey(typeName))
                {
                    groups[typeName] = new List<ElementId>();
                }

                groups[typeName].Add(element.Id);
            }
        }

        private static void CollectToposolids(Document doc, Dictionary<string, List<ElementId>> groups)
        {
            Type? toposolidType = typeof(Document).Assembly.GetType("Autodesk.Revit.DB.Toposolid");
            if (toposolidType == null)
            {
                throw new InvalidOperationException("Toposolid runtime type was not found. This add-in targets Revit 2026 only.");
            }

            var instances = new FilteredElementCollector(doc)
                .OfClass(toposolidType)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (Element element in instances)
            {
                ElementId typeId = element.GetTypeId();
                if (typeId == null || typeId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Element? typeElement = doc.GetElement(typeId);
                if (typeElement == null)
                {
                    continue;
                }

                string typeName = $"{typeElement.Category?.Name ?? "Toposolid"} - {typeElement.Name}";

                if (!groups.ContainsKey(typeName))
                {
                    groups[typeName] = new List<ElementId>();
                }

                groups[typeName].Add(element.Id);
            }
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

            while (sanitized.Contains("__", StringComparison.Ordinal))
            {
                sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
            }

            return sanitized.Trim('_');
        }

        private static bool IsExpectedLinkedModelExportException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or IOException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        private readonly struct ExportedGroupResult
        {
            public ExportedGroupResult(string typeName, string filePath, List<ElementId> elementIds)
            {
                TypeName = typeName;
                FilePath = filePath;
                ElementIds = elementIds;
            }

            public string TypeName { get; }
            public string FilePath { get; }
            public List<ElementId> ElementIds { get; }
            public int ElementCount => ElementIds.Count;
        }
    }
}
