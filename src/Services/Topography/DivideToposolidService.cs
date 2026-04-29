using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class DivideToposolidService : IDivideToposolidService
    {
        private readonly ITransactionService _transactionService;
        private readonly IGeometryBoundaryService _geometryBoundaryService;
        private readonly ISlabService _slabService;

        public DivideToposolidService(
            ITransactionService transactionService,
            IGeometryBoundaryService geometryBoundaryService,
            ISlabService slabService)
        {
            _transactionService = transactionService;
            _geometryBoundaryService = geometryBoundaryService;
            _slabService = slabService;
        }

        public void DivideToposolids(Document doc, IList<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            int processed = 0;

            foreach (Element element in elements)
            {
                if (element == null || !element.IsValidObject) continue;
                processed++;
                double pct = (double)processed / elements.Count * 95;
                reporter.Report($"Processing {processed} of {elements.Count}...", pct);

                ElementId originalId = element.Id;

                try
                {
                    if (element is not Toposolid)
                    {
                        reporter.LogWarning($"ID {originalId}: not a Toposolid, skipped.");
                        continue;
                    }

                    DivideSingleToposolid(doc, element, reporter);
                }
                catch (Exception ex) when (IsExpectedDivideException(ex))
                {
                    reporter.LogError($"ID {originalId}: failed - {ex.Message}");
                }
            }

            reporter.Report("Complete", 100);
        }

        private void DivideSingleToposolid(Document doc, Element element, IProgressReporter reporter)
        {
            var toposolid = (Toposolid)element;
            ElementId originalId = toposolid.Id;

            // 1. Read compound structure layers
            var originalType = doc.GetElement(toposolid.GetTypeId()) as HostObjAttributes;
            if (originalType == null)
            {
                reporter.LogWarning($"ID {originalId}: could not resolve type, skipped.");
                return;
            }

            CompoundStructure? cs = originalType.GetCompoundStructure();
            if (cs == null)
            {
                reporter.LogWarning($"ID {originalId}: no compound structure, skipped.");
                return;
            }

            IList<CompoundStructureLayer> layers = cs.GetLayers();
            if (layers.Count <= 1)
            {
                reporter.Log($"ID {originalId}: already single layer, skipped.");
                return;
            }

            reporter.Log($"ID {originalId}: {layers.Count} layers found, dividing...");

            // 2. Snapshot vertices before any modifications
            List<VertexSnapshot> vertexSnapshots = SnapshotVertices(element);

            // 3. Extract boundary loops
            IList<CurveLoop> loops = _geometryBoundaryService.ExtractLoops(element);
            if (loops.Count == 0)
            {
                reporter.LogWarning($"ID {originalId}: no boundary loops found, skipped.");
                return;
            }

            // 4. Read original properties
            ElementId levelId = toposolid.LevelId;
            double originalHeightOffset = GetHeightOffset(element);
            string originalTypeName = originalType.Name;

            // 5. Compute per-layer cumulative offsets
            var layerData = new List<(double CumulativeOffset, CompoundStructureLayer Layer, int Index)>();
            double cumulative = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                layerData.Add((cumulative, layers[i], i));
                cumulative += layers[i].Width;
            }

            // 6. Transaction 1: Create new toposolids + delete original
            var newToposolidEntries = new List<(ElementId Id, double CumulativeOffset)>();

            _transactionService.Run(doc, "Divide Toposolid - Create Layers", currentDoc =>
            {
                foreach (var (cumulativeOffset, layer, index) in layerData)
                {
                    // Find or create single-layer type
                    ElementId newTypeId = FindOrCreateSingleLayerType(
                        currentDoc, originalType, layer, index, originalTypeName);

                    // Create new toposolid with same boundary
                    Toposolid newToposolid = Toposolid.Create(currentDoc, loops, newTypeId, levelId);

                    // Set height offset: lower layers are pushed down
                    double newHeightOffset = originalHeightOffset - cumulativeOffset;
                    SetHeightOffset(newToposolid, newHeightOffset);

                    newToposolidEntries.Add((newToposolid.Id, cumulativeOffset));

                    string materialName = GetMaterialName(currentDoc, layer.MaterialId);
                    reporter.Log($"  Layer {index + 1}: type created, material={materialName}, " +
                        $"thickness={layer.Width:F4} ft, offset={cumulativeOffset:F4} ft");
                }

                currentDoc.Delete(originalId);
            });

            // 7. Transaction 2: Copy shape to each new toposolid
            if (vertexSnapshots.Count > 0 && newToposolidEntries.Count > 0)
            {
                var outputSummaries = new List<string>();

                _transactionService.Run(doc, "Divide Toposolid - Copy Shape", currentDoc =>
                {
                    foreach (var (newId, cumulativeOffset) in newToposolidEntries)
                    {
                        Element? newElement = currentDoc.GetElement(newId);
                        if (newElement == null) continue;

                        SlabShapeEditor? editor = _slabService.GetEditor(newElement);
                        if (editor == null) continue;

                        if (!editor.IsEnabled)
                        {
                            editor.Enable();
                        }

                        int interiorPointsAdded = 0;
                        int boundaryVerticesAdjusted = 0;

                        foreach (VertexSnapshot snapshot in vertexSnapshots)
                        {
                            double adjustedZ = snapshot.Position.Z - cumulativeOffset;
                            XYZ targetPosition = new XYZ(
                                snapshot.Position.X, snapshot.Position.Y, adjustedZ);

                            if (snapshot.VertexType == SlabShapeVertexType.Interior)
                            {
                                try
                                {
                                    editor.AddPoint(targetPosition);
                                    interiorPointsAdded++;
                                }
                                catch (RevitExceptions.InvalidOperationException)
                                {
                                    // Revit rejects point as non-interior after mesh regeneration
                                }
                                catch (Exception ex) when (IsExpectedDivideException(ex))
                                {
                                    // Protected vertex or other expected failure
                                }
                            }
                            else if (snapshot.VertexType is SlabShapeVertexType.Edge
                                or SlabShapeVertexType.Corner)
                            {
                                if (TryAdjustBoundaryVertex(editor, targetPosition))
                                {
                                    boundaryVerticesAdjusted++;
                                }
                            }
                        }

                        outputSummaries.Add(
                            $"  Output {newId}: added {interiorPointsAdded} interior points, " +
                            $"adjusted {boundaryVerticesAdjusted} boundary vertices.");
                    }
                });

                foreach (string summary in outputSummaries)
                {
                    reporter.Log(summary);
                }
            }

            reporter.Log($"ID {originalId}: divided into {newToposolidEntries.Count} layers " +
                $"[{FormatElementIds(newToposolidEntries.Select(e => e.Id))}].");
        }

        private static ElementId FindOrCreateSingleLayerType(
            Document doc,
            HostObjAttributes originalType,
            CompoundStructureLayer layer,
            int layerIndex,
            string originalTypeName)
        {
            string newTypeName = $"{originalTypeName} - Layer {layerIndex + 1}";

            // Check if type already exists
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(ToposolidType))
                .Cast<ToposolidType>()
                .FirstOrDefault(t => t.Name == newTypeName);

            if (existing != null)
            {
                return existing.Id;
            }

            // Duplicate and modify
            var newType = originalType.Duplicate(newTypeName) as HostObjAttributes;
            if (newType == null)
            {
                return originalType.Id;
            }

            CompoundStructure singleLayerCs = CompoundStructure.CreateSingleLayerCompoundStructure(
                layer.Function, layer.Width, layer.MaterialId);
            newType.SetCompoundStructure(singleLayerCs);

            return newType.Id;
        }

        private static string GetMaterialName(Document doc, ElementId materialId)
        {
            if (materialId == ElementId.InvalidElementId) return "(none)";
            var material = doc.GetElement(materialId) as Material;
            return material?.Name ?? "(unknown)";
        }

        private List<VertexSnapshot> SnapshotVertices(Element element)
        {
            SlabShapeEditor? editor = _slabService.GetEditor(element);
            if (editor == null || !editor.IsEnabled)
            {
                return new List<VertexSnapshot>();
            }

            var snapshots = new List<VertexSnapshot>();

            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                snapshots.Add(new VertexSnapshot(vertex.Position, vertex.VertexType));
            }

            return snapshots;
        }

        private static double GetHeightOffset(Element element)
        {
            Parameter? floorParam = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (floorParam != null) return floorParam.AsDouble();

            Parameter? topoParam = element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
            return topoParam?.AsDouble() ?? 0.0;
        }

        private static void SetHeightOffset(Element element, double value)
        {
            Parameter? param = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);

            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        private static bool TryAdjustBoundaryVertex(SlabShapeEditor editor, XYZ targetPosition)
        {
            const double tolerance = 1e-4;

            SlabShapeVertex? matchingVertex = editor.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .Where(vertex => vertex.VertexType != SlabShapeVertexType.Interior)
                .OrderBy(vertex => HorizontalDistance(vertex.Position, targetPosition))
                .FirstOrDefault();

            if (matchingVertex == null)
            {
                return false;
            }

            if (HorizontalDistance(matchingVertex.Position, targetPosition) > tolerance)
            {
                return false;
            }

            double deltaZ = targetPosition.Z - matchingVertex.Position.Z;
            if (Math.Abs(deltaZ) <= 1e-9)
            {
                return false;
            }

            try
            {
                editor.ModifySubElement(matchingVertex, deltaZ);
                return true;
            }
            catch (Exception ex) when (IsExpectedDivideException(ex))
            {
                return false;
            }
        }

        private static double HorizontalDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string FormatElementIds(IEnumerable<ElementId> elementIds)
        {
            return string.Join(", ", elementIds.Select(id => id.ToString()));
        }

        private static bool IsExpectedDivideException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        private readonly struct VertexSnapshot
        {
            public VertexSnapshot(XYZ position, SlabShapeVertexType vertexType)
            {
                Position = position;
                VertexType = vertexType;
            }

            public XYZ Position { get; }
            public SlabShapeVertexType VertexType { get; }
        }
    }
}
