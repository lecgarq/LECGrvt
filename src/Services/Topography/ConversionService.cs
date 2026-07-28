using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class ConversionService
    {
        private readonly ITransactionService _transactionService;
        private readonly GeometryBoundaryService _boundaryService;
        private readonly SlabService _slabService;

        public ConversionService(
            ITransactionService transactionService,
            GeometryBoundaryService boundaryService,
            SlabService slabService)
        {
            _transactionService = transactionService;
            _boundaryService = boundaryService;
            _slabService = slabService;
        }

        public void ConvertFloorToToposolid(Document doc, IList<Element> floors, ElementId toposolidTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(floors);
            ArgumentNullException.ThrowIfNull(toposolidTypeId);
            ArgumentNullException.ThrowIfNull(levelId);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int failCount = 0;
            string targetTypeName = GetElementName(doc, toposolidTypeId);

            for (int i = 0; i < floors.Count; i++)
            {
                Element floor = floors[i];
                if (floor == null || !floor.IsValidObject) continue;

                double percent = (double)(i + 1) / floors.Count * 100;
                reporter.Report($"Converting floor {i + 1} of {floors.Count}...", percent);

                ElementId originalId = floor.Id;

                try
                {
                    ConvertSingleFloorToToposolid(doc, floor, toposolidTypeId, levelId, deleteSource, reporter);
                    successCount++;
                }
                catch (Exception ex) when (IsExpectedConversionException(ex))
                {
                    failCount++;
                    reporter.LogError($"Failed to convert floor ID {originalId} to '{targetTypeName}': {ex.Message}");
                }
            }

            reporter.Log($"Conversion complete: {successCount} succeeded, {failCount} failed.");
        }

        public void ConvertToposolidToFloor(Document doc, IList<Element> toposolids, ElementId floorTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(toposolids);
            ArgumentNullException.ThrowIfNull(floorTypeId);
            ArgumentNullException.ThrowIfNull(levelId);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int failCount = 0;
            string targetTypeName = GetElementName(doc, floorTypeId);

            for (int i = 0; i < toposolids.Count; i++)
            {
                Element toposolid = toposolids[i];
                if (toposolid == null || !toposolid.IsValidObject) continue;

                double percent = (double)(i + 1) / toposolids.Count * 100;
                reporter.Report($"Converting toposolid {i + 1} of {toposolids.Count}...", percent);

                ElementId originalId = toposolid.Id;

                try
                {
                    ConvertSingleToposolidToFloor(doc, toposolid, floorTypeId, levelId, deleteSource, reporter);
                    successCount++;
                }
                catch (Exception ex) when (IsExpectedConversionException(ex))
                {
                    failCount++;
                    reporter.LogError($"Failed to convert toposolid ID {originalId} to '{targetTypeName}': {ex.Message}");
                }
            }

            reporter.Log($"Conversion complete: {successCount} succeeded, {failCount} failed.");
        }

        public IList<ElementType> GetFloorTypes(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<ElementType>()
                .OrderBy(t => t.Name)
                .ToList();
        }

        public IList<ElementType> GetToposolidTypes(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ToposolidType))
                .Cast<ElementType>()
                .OrderBy(t => t.Name)
                .ToList();
        }

        public IList<Level> GetLevels(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        public ElementType? FindMatchingType(string sourceName, IList<ElementType> targetTypes)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || targetTypes == null || targetTypes.Count == 0)
                return null;

            return targetTypes
                .Select(type => new
                {
                    Type = type,
                    Score = ScoreTypeMatch(sourceName, type.Name)
                })
                .Where(entry => entry.Score > 0)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Type.Name.Length)
                .Select(entry => entry.Type)
                .FirstOrDefault();
        }

        // ============================================
        // Floor -> Toposolid
        // ============================================

        private void ConvertSingleFloorToToposolid(Document doc, Element floor, ElementId toposolidTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter)
        {
            ElementId originalId = floor.Id;
            Level targetLevel = doc.GetElement(levelId) as Level ?? throw new InvalidOperationException("Target level not found.");
            string targetTypeName = GetElementName(doc, toposolidTypeId);

            // 1. Extract geometry data before any transactions
            IList<CurveLoop> loops = _boundaryService.AlignLoopsToCommonPlane(_boundaryService.ExtractLoops(floor));
            if (loops.Count == 0)
            {
                reporter.LogWarning($"Floor ID {originalId}: skipped for '{targetTypeName}' because no boundary loops were found.");
                return;
            }

            List<XYZ> interiorPoints = _slabService.GetInteriorVertexPositions(floor);

            double targetHeightOffset = GetTargetHeightOffset(doc, floor, targetLevel);

            // 2. Build absolute-Z points for Toposolid.Create
            //    Toposolid.Create expects interior points with absolute Z coordinates
            IList<XYZ> topoPoints = BuildAbsolutePoints(interiorPoints, targetLevel.Elevation, targetHeightOffset);

            // 3. Create Toposolid in a single transaction
            _transactionService.Run(doc, "Convert Floor to Toposolid", currentDoc =>
            {
                Toposolid newToposolid = Toposolid.Create(currentDoc, loops, topoPoints, toposolidTypeId, levelId);

                // Set height offset on the new Toposolid
                SetHeightOffset(newToposolid, targetHeightOffset);

                reporter.Log($"Floor ID {originalId} -> Toposolid ID {newToposolid.Id} | type '{targetTypeName}' | level '{targetLevel.Name}' | vertex count {interiorPoints.Count}");

                if (deleteSource)
                {
                    currentDoc.Delete(originalId);
                    reporter.Log($"  Deleted source floor ID {originalId}");
                }
            });
        }

        // ============================================
        // Toposolid -> Floor
        // ============================================

        private void ConvertSingleToposolidToFloor(Document doc, Element toposolid, ElementId floorTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter)
        {
            Level targetLevel = doc.GetElement(levelId) as Level ?? throw new InvalidOperationException("Target level not found.");
            string targetTypeName = GetElementName(doc, floorTypeId);

            // 1. Extract geometry data before any transactions
            IList<CurveLoop> loops = _boundaryService.AlignLoopsToCommonPlane(_boundaryService.ExtractLoops(toposolid));
            if (loops.Count == 0)
            {
                reporter.LogWarning($"Toposolid ID {toposolid.Id}: skipped for '{targetTypeName}' because no boundary loops were found.");
                return;
            }

            List<XYZ> interiorPoints = _slabService.GetInteriorVertexPositions(toposolid);

            double targetHeightOffset = GetTargetHeightOffset(doc, toposolid, targetLevel);

            _transactionService.Run(doc, "Convert Toposolid to Floor", currentDoc =>
            {
                try
                {
                    reporter.Log($"  Step 1: Creating floor with {loops.Count} loops...");
                    Floor newFloor = Floor.Create(currentDoc, loops, floorTypeId, levelId);
                    if (newFloor == null) throw new InvalidOperationException("Revit returned null when creating the floor.");

                    // CRITICAL: Regenerate document to ensure the new floor's geometry and SlabShapeEditor are initialized
                    currentDoc.Regenerate();

                    ElementId newFloorId = newFloor.Id;
                    reporter.Log($"  Step 2: Floor created with ID {newFloorId}.");

                    reporter.Log("  Step 3: Setting height offset...");
                    SetHeightOffset(newFloor, targetHeightOffset);

                    if (interiorPoints.Count > 0)
                    {
                        reporter.Log($"  Step 4: Applying {interiorPoints.Count} shape points...");
                        SlabShapeEditor? editor = _slabService.GetEditor(newFloor);
                        if (editor != null)
                        {
                            if (!editor.IsEnabled) editor.Enable();
                            foreach (XYZ vertex in interiorPoints)
                            {
                                double relativeZ = vertex.Z - (targetLevel.Elevation + targetHeightOffset);
                                editor.AddPoint(new XYZ(vertex.X, vertex.Y, relativeZ));
                            }
                        }
                    }

                    reporter.Log($"  Step 5: Completion check for Toposolid ID {toposolid.Id} -> Floor ID {newFloorId}");

                    if (deleteSource)
                    {
                        reporter.Log($"  Step 6: Deleting source element {toposolid.Id}...");
                        currentDoc.Delete(toposolid.Id);
                    }
                }
                catch (Exception ex)
                {
                    reporter.LogError($"  Inner Failure Trace: {ex.GetType().Name} - {ex.Message}");
                    throw; // Rethrow to be caught by the outer loop
                }
            });
        }

        // ============================================
        // Helpers
        // ============================================

        private static double GetHeightOffset(Element element)
        {
            Parameter? floorParam = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (floorParam != null) return floorParam.AsDouble();

            Parameter? topoParam = element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
            return topoParam?.AsDouble() ?? 0.0;
        }

        private static double GetTargetHeightOffset(Document doc, Element sourceElement, Level targetLevel)
        {
            Level sourceLevel = GetElementLevel(sourceElement)
                ?? throw new InvalidOperationException($"Source level not found for element ID {sourceElement.Id}.");

            double sourceBaseElevation = sourceLevel.Elevation + GetHeightOffset(sourceElement);
            return sourceBaseElevation - targetLevel.Elevation;
        }

        private static void SetHeightOffset(Element element, double offset)
        {
            Parameter? param = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);

            if (param != null && !param.IsReadOnly)
            {
                param.Set(offset);
            }
        }

        private static IList<XYZ> BuildAbsolutePoints(List<XYZ> vertexPositions, double levelElevation, double heightOffset)
        {
            var absolutePoints = new List<XYZ>(vertexPositions.Count);

            foreach (XYZ vertex in vertexPositions)
            {
                // vertexPositions are already absolute (from SlabShapeVertex.Position).
                // Toposolid.Create expects absolute points. 
                // We just pass them through.
                double absoluteZ = vertex.Z;
                absolutePoints.Add(new XYZ(vertex.X, vertex.Y, absoluteZ));
            }

            return absolutePoints;
        }

        private static Level? GetElementLevel(Element element)
        {
            Parameter? levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);

            if (levelParam == null)
            {
                return null;
            }

            ElementId levelId = levelParam.AsElementId();
            if (levelId == ElementId.InvalidElementId)
            {
                return null;
            }

            return element.Document.GetElement(levelId) as Level;
        }

        private static string GetElementName(Document doc, ElementId elementId)
        {
            Element? element = doc.GetElement(elementId);
            return element?.Name ?? elementId.ToString();
        }

        private static int ScoreTypeMatch(string sourceName, string targetName)
        {
            if (string.Equals(sourceName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return int.MaxValue;
            }

            string normalizedSource = NormalizeName(sourceName);
            string normalizedTarget = NormalizeName(targetName);
            if (string.IsNullOrEmpty(normalizedSource) || string.IsNullOrEmpty(normalizedTarget))
            {
                return 0;
            }

            int score = 0;
            if (normalizedSource.Contains(normalizedTarget, StringComparison.Ordinal))
            {
                score += 80;
            }

            if (normalizedTarget.Contains(normalizedSource, StringComparison.Ordinal))
            {
                score += 80;
            }

            string[] sourceTokens = normalizedSource.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] targetTokens = normalizedTarget.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int sharedTokenCount = sourceTokens.Intersect(targetTokens).Count();
            score += sharedTokenCount * 100;
            score += GetSharedPrefixLength(normalizedSource, normalizedTarget) * 2;

            return score;
        }

        private static string NormalizeName(string value)
        {
            var characters = value
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray();

            return string.Join(" ", new string(characters)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static int GetSharedPrefixLength(string left, string right)
        {
            int max = Math.Min(left.Length, right.Length);
            int count = 0;

            while (count < max && left[count] == right[count])
            {
                count++;
            }

            return count;
        }

        private static bool IsExpectedConversionException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
