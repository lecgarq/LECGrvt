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
        private readonly SplitBoundariesService _surfaceTransferService;

        public ConversionService(
            ITransactionService transactionService,
            GeometryBoundaryService boundaryService,
            SplitBoundariesService surfaceTransferService)
        {
            _transactionService = transactionService;
            _boundaryService = boundaryService;
            _surfaceTransferService = surfaceTransferService;
        }

        public void ConvertFloorToToposolid(Document doc, IList<Element> floors, ElementId toposolidTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter,
            bool createTypeFromSource = false, bool preserveSourceLevel = false)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(floors);
            ArgumentNullException.ThrowIfNull(toposolidTypeId);
            ArgumentNullException.ThrowIfNull(levelId);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int failCount = 0;
            string targetTypeName = createTypeFromSource ? "Create Type" : GetElementName(doc, toposolidTypeId);
            var createdTypeIds = new Dictionary<long, ElementId>();

            for (int i = 0; i < floors.Count; i++)
            {
                Element floor = floors[i];
                if (floor == null || !floor.IsValidObject) continue;

                double percent = (double)(i + 1) / floors.Count * 100;
                reporter.Report($"Converting floor {i + 1} of {floors.Count}...", percent);

                ElementId originalId = floor.Id;

                try
                {
                    ConvertSingleFloorToToposolid(doc, floor, toposolidTypeId, levelId,
                        deleteSource, reporter, createTypeFromSource, preserveSourceLevel,
                        createdTypeIds);
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

        public void ConvertToposolidToFloor(Document doc, IList<Element> toposolids, ElementId floorTypeId, ElementId levelId, bool deleteSource, IProgressReporter reporter,
            bool createTypeFromSource = false, bool preserveSourceLevel = false)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(toposolids);
            ArgumentNullException.ThrowIfNull(floorTypeId);
            ArgumentNullException.ThrowIfNull(levelId);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int failCount = 0;
            string targetTypeName = createTypeFromSource ? "Create Type" : GetElementName(doc, floorTypeId);
            var createdTypeIds = new Dictionary<long, ElementId>();

            for (int i = 0; i < toposolids.Count; i++)
            {
                Element toposolid = toposolids[i];
                if (toposolid == null || !toposolid.IsValidObject) continue;

                double percent = (double)(i + 1) / toposolids.Count * 100;
                reporter.Report($"Converting toposolid {i + 1} of {toposolids.Count}...", percent);

                ElementId originalId = toposolid.Id;

                try
                {
                    ConvertSingleToposolidToFloor(doc, toposolid, floorTypeId, levelId,
                        deleteSource, reporter, createTypeFromSource, preserveSourceLevel,
                        createdTypeIds);
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

        private void ConvertSingleFloorToToposolid(Document doc, Element floor,
            ElementId toposolidTypeId, ElementId levelId, bool deleteSource,
            IProgressReporter reporter, bool createTypeFromSource,
            bool preserveSourceLevel, IDictionary<long, ElementId> createdTypeIds)
        {
            ElementId originalId = floor.Id;
            ElementId sourceTypeId = floor.GetTypeId();
            ElementId targetLevelId = preserveSourceLevel
                ? GetElementLevel(floor)?.Id
                    ?? throw new InvalidOperationException($"Source level not found for element ID {floor.Id}.")
                : levelId;
            Level targetLevel = doc.GetElement(targetLevelId) as Level
                ?? throw new InvalidOperationException("Target level not found.");
            ElementId resolvedTypeId = createTypeFromSource
                ? ElementId.InvalidElementId
                : toposolidTypeId;
            if (createTypeFromSource
                && createdTypeIds.TryGetValue(sourceTypeId.Value, out ElementId? cachedTypeId)
                && cachedTypeId != null)
                resolvedTypeId = cachedTypeId;
            string targetTypeName = createTypeFromSource
                ? $"type created from '{GetElementName(doc, sourceTypeId)}'"
                : GetElementName(doc, resolvedTypeId);

            // 1. Extract geometry data before any transactions
            IList<CurveLoop> loops = _boundaryService.AlignLoopsToCommonPlane(_boundaryService.ExtractLoops(floor));
            if (loops.Count == 0)
            {
                reporter.LogWarning($"Floor ID {originalId}: skipped for '{targetTypeName}' because no boundary loops were found.");
                return;
            }

            var source = (Floor)floor;
            SplitBoundariesService.SurfaceTransferSnapshot surface =
                _surfaceTransferService.CaptureSurfaceTransfer(source);
            List<CurveLoop> transferProfile = _surfaceTransferService.PrepareSurfaceTransferProfile(
                loops, surface, doc.Application.ShortCurveTolerance);
            double targetHeightOffset = GetTargetHeightOffset(doc, floor, targetLevel);
            bool sourceWasPinned = floor.Pinned;
            ElementId createdTypeId = ElementId.InvalidElementId;

            _transactionService.Run(doc, "Convert Floor to Toposolid", currentDoc =>
            {
                if (createTypeFromSource && resolvedTypeId == ElementId.InvalidElementId)
                {
                    createdTypeId = CreateTypeFromSource(
                        currentDoc, floor, typeof(ToposolidType), reporter);
                    resolvedTypeId = createdTypeId;
                    targetTypeName = GetElementName(currentDoc, resolvedTypeId);
                }

                Toposolid newToposolid = Toposolid.Create(
                    currentDoc, transferProfile, resolvedTypeId, targetLevelId);
                SetHeightOffset(newToposolid, targetHeightOffset);
                currentDoc.Regenerate();
                _surfaceTransferService.RestoreTransferredSurface(
                    currentDoc, newToposolid, transferProfile, surface, reporter);
                newToposolid.Pinned = sourceWasPinned;

                reporter.Log($"Floor ID {originalId} -> Toposolid ID {newToposolid.Id} | type '{targetTypeName}' | level '{targetLevel.Name}' | preserved shape points {surface.Vertices.Count}");

                if (deleteSource)
                {
                    if (floor.Pinned) floor.Pinned = false;
                    currentDoc.Delete(originalId);
                    reporter.Log($"  Deleted source floor ID {originalId}");
                }
            });

            if (createdTypeId != ElementId.InvalidElementId)
                createdTypeIds[sourceTypeId.Value] = createdTypeId;
        }

        // ============================================
        // Toposolid -> Floor
        // ============================================

        private void ConvertSingleToposolidToFloor(Document doc, Element toposolid,
            ElementId floorTypeId, ElementId levelId, bool deleteSource,
            IProgressReporter reporter, bool createTypeFromSource,
            bool preserveSourceLevel, IDictionary<long, ElementId> createdTypeIds)
        {
            ElementId sourceTypeId = toposolid.GetTypeId();
            ElementId targetLevelId = preserveSourceLevel
                ? GetElementLevel(toposolid)?.Id
                    ?? throw new InvalidOperationException($"Source level not found for element ID {toposolid.Id}.")
                : levelId;
            Level targetLevel = doc.GetElement(targetLevelId) as Level
                ?? throw new InvalidOperationException("Target level not found.");
            ElementId resolvedTypeId = createTypeFromSource
                ? ElementId.InvalidElementId
                : floorTypeId;
            if (createTypeFromSource
                && createdTypeIds.TryGetValue(sourceTypeId.Value, out ElementId? cachedTypeId)
                && cachedTypeId != null)
                resolvedTypeId = cachedTypeId;
            string targetTypeName = createTypeFromSource
                ? $"type created from '{GetElementName(doc, sourceTypeId)}'"
                : GetElementName(doc, resolvedTypeId);

            // 1. Extract geometry data before any transactions
            IList<CurveLoop> loops = _boundaryService.AlignLoopsToCommonPlane(_boundaryService.ExtractLoops(toposolid));
            if (loops.Count == 0)
            {
                reporter.LogWarning($"Toposolid ID {toposolid.Id}: skipped for '{targetTypeName}' because no boundary loops were found.");
                return;
            }

            var source = (Toposolid)toposolid;
            SplitBoundariesService.SurfaceTransferSnapshot surface =
                _surfaceTransferService.CaptureSurfaceTransfer(source);
            List<CurveLoop> transferProfile = _surfaceTransferService.PrepareSurfaceTransferProfile(
                loops, surface, doc.Application.ShortCurveTolerance);
            double targetHeightOffset = GetTargetHeightOffset(doc, toposolid, targetLevel);
            bool sourceWasPinned = toposolid.Pinned;
            ElementId createdTypeId = ElementId.InvalidElementId;

            _transactionService.Run(doc, "Convert Toposolid to Floor", currentDoc =>
            {
                try
                {
                    if (createTypeFromSource && resolvedTypeId == ElementId.InvalidElementId)
                    {
                        createdTypeId = CreateTypeFromSource(
                            currentDoc, toposolid, typeof(FloorType), reporter);
                        resolvedTypeId = createdTypeId;
                        targetTypeName = GetElementName(currentDoc, resolvedTypeId);
                    }

                    reporter.Log($"  Step 1: Creating floor with {transferProfile.Count} loops...");
                    Floor newFloor = Floor.Create(currentDoc, transferProfile, resolvedTypeId, targetLevelId);
                    if (newFloor == null) throw new InvalidOperationException("Revit returned null when creating the floor.");

                    // CRITICAL: Regenerate document to ensure the new floor's geometry and SlabShapeEditor are initialized
                    currentDoc.Regenerate();

                    ElementId newFloorId = newFloor.Id;
                    reporter.Log($"  Step 2: Floor created with ID {newFloorId}.");

                    reporter.Log("  Step 3: Setting height offset...");
                    SetHeightOffset(newFloor, targetHeightOffset);

                    reporter.Log($"  Step 4: Restoring {surface.Vertices.Count} shape points...");
                    _surfaceTransferService.RestoreTransferredSurface(
                        currentDoc, newFloor, transferProfile, surface, reporter);
                    newFloor.Pinned = sourceWasPinned;

                    reporter.Log($"  Step 5: Completion check for Toposolid ID {toposolid.Id} -> Floor ID {newFloorId}");

                    if (deleteSource)
                    {
                        reporter.Log($"  Step 6: Deleting source element {toposolid.Id}...");
                        if (toposolid.Pinned) toposolid.Pinned = false;
                        currentDoc.Delete(toposolid.Id);
                    }
                }
                catch (Exception ex)
                {
                    reporter.LogError($"  Inner Failure Trace: {ex.GetType().Name} - {ex.Message}");
                    throw; // Rethrow to be caught by the outer loop
                }
            });

            if (createdTypeId != ElementId.InvalidElementId)
                createdTypeIds[sourceTypeId.Value] = createdTypeId;
        }

        // ============================================
        // Helpers
        // ============================================

        private static ElementId CreateTypeFromSource(Document doc, Element sourceElement,
            Type targetTypeClass, IProgressReporter reporter)
        {
            var sourceType = doc.GetElement(sourceElement.GetTypeId()) as HostObjAttributes
                ?? throw new InvalidOperationException(
                    $"Source type not found for element ID {sourceElement.Id}.");
            CompoundStructure sourceStructure = sourceType.GetCompoundStructure()
                ?? throw new InvalidOperationException(
                    $"Source type '{sourceType.Name}' has no compound structure to copy.");

            HostObjAttributes? seedType = new FilteredElementCollector(doc)
                .OfClass(targetTypeClass)
                .Cast<HostObjAttributes>()
                .Where(type => type is not FloorType floorType || !floorType.IsFoundationSlab)
                .OrderBy(type => type.Name)
                .FirstOrDefault();
            if (seedType == null)
            {
                string targetLabel = targetTypeClass == typeof(FloorType) ? "Floor" : "Toposolid";
                throw new InvalidOperationException(
                    $"No {targetLabel} type is available to use as the base for Create Type.");
            }

            string newTypeName = GetUniqueTypeName(doc, targetTypeClass, sourceType.Name);
            var createdType = seedType.Duplicate(newTypeName) as HostObjAttributes
                ?? throw new InvalidOperationException(
                    $"Revit could not create target type '{newTypeName}'.");
            CompoundStructure targetStructure = BuildTargetCompoundStructure(
                sourceStructure, seedType);
            if (!targetStructure.IsValid(doc,
                    out IDictionary<int, CompoundStructureError>? structureErrors,
                    out IDictionary<int, int>? twoLayerErrors))
            {
                string details = structureErrors == null || structureErrors.Count == 0
                    ? "unknown compound-structure error"
                    : string.Join(", ", structureErrors
                        .OrderBy(entry => entry.Key)
                        .Select(entry => $"layer {entry.Key}: {entry.Value}"));
                if (twoLayerErrors != null && twoLayerErrors.Count > 0)
                {
                    details += "; region ordering " + string.Join(", ",
                        twoLayerErrors.Select(entry => $"{entry.Key}->{entry.Value}"));
                }
                throw new InvalidOperationException(
                    $"The source layers cannot form a valid target type: {details}.");
            }
            createdType.SetCompoundStructure(targetStructure);
            VerifyCompoundStructure(sourceType, createdType);

            IList<CompoundStructureLayer> layers = sourceStructure.GetLayers();
            reporter.Log($"  Created type '{newTypeName}' with {layers.Count} source layers "
                + $"and total thickness {sourceStructure.GetWidth():F6}.");
            return createdType.Id;
        }

        private static CompoundStructure BuildTargetCompoundStructure(
            CompoundStructure sourceStructure, HostObjAttributes seedType)
        {
            CompoundStructure targetStructure = seedType.GetCompoundStructure()
                ?? throw new InvalidOperationException(
                    $"Base target type '{seedType.Name}' has no compound structure.");

            // Revit stores category-specific end-cap data in a compound structure. Starting with
            // the target category's valid structure and resetting its layers preserves those
            // target-only settings while transferring the complete ordered physical layer list.
            List<CompoundStructureLayer> layers = sourceStructure.GetLayers()
                .Select(layer => new CompoundStructureLayer(layer))
                .ToList();
            targetStructure.SetLayers(layers);
            targetStructure.SetNumberOfShellLayers(
                ShellLayerType.Exterior,
                sourceStructure.GetNumberOfShellLayers(ShellLayerType.Exterior));
            targetStructure.SetNumberOfShellLayers(
                ShellLayerType.Interior,
                sourceStructure.GetNumberOfShellLayers(ShellLayerType.Interior));
            if (sourceStructure.VariableLayerIndex >= 0)
                targetStructure.VariableLayerIndex = sourceStructure.VariableLayerIndex;
            if (sourceStructure.StructuralMaterialIndex >= 0)
                targetStructure.StructuralMaterialIndex = sourceStructure.StructuralMaterialIndex;
            return targetStructure;
        }

        private static string GetUniqueTypeName(Document doc, Type targetTypeClass,
            string sourceTypeName)
        {
            var existingNames = new FilteredElementCollector(doc)
                .OfClass(targetTypeClass)
                .Cast<ElementType>()
                .Select(type => type.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(sourceTypeName))
                return sourceTypeName;

            string convertedName = $"{sourceTypeName} (Converted)";
            if (!existingNames.Contains(convertedName))
                return convertedName;

            int suffix = 2;
            while (existingNames.Contains($"{sourceTypeName} (Converted {suffix})"))
                suffix++;
            return $"{sourceTypeName} (Converted {suffix})";
        }

        private static void VerifyCompoundStructure(HostObjAttributes sourceType,
            HostObjAttributes targetType)
        {
            CompoundStructure sourceStructure = sourceType.GetCompoundStructure()
                ?? throw new InvalidOperationException("Source compound structure is unavailable.");
            CompoundStructure targetStructure = targetType.GetCompoundStructure()
                ?? throw new InvalidOperationException(
                    $"Created type '{targetType.Name}' has no compound structure.");
            IList<CompoundStructureLayer> sourceLayers = sourceStructure.GetLayers();
            IList<CompoundStructureLayer> targetLayers = targetStructure.GetLayers();

            if (sourceLayers.Count != targetLayers.Count)
            {
                throw new InvalidOperationException(
                    $"Created type '{targetType.Name}' has {targetLayers.Count} layers; "
                    + $"the source has {sourceLayers.Count}.");
            }

            if (sourceStructure.GetNumberOfShellLayers(ShellLayerType.Exterior)
                    != targetStructure.GetNumberOfShellLayers(ShellLayerType.Exterior)
                || sourceStructure.GetNumberOfShellLayers(ShellLayerType.Interior)
                    != targetStructure.GetNumberOfShellLayers(ShellLayerType.Interior)
                || sourceStructure.VariableLayerIndex != targetStructure.VariableLayerIndex
                || sourceStructure.StructuralMaterialIndex != targetStructure.StructuralMaterialIndex)
            {
                throw new InvalidOperationException(
                    $"Created type '{targetType.Name}' did not preserve the source layer configuration.");
            }

            for (int index = 0; index < sourceLayers.Count; index++)
            {
                CompoundStructureLayer sourceLayer = sourceLayers[index];
                CompoundStructureLayer targetLayer = targetLayers[index];
                if (Math.Abs(sourceLayer.Width - targetLayer.Width) > 1e-9
                    || sourceLayer.MaterialId != targetLayer.MaterialId
                    || sourceLayer.Function != targetLayer.Function)
                {
                    throw new InvalidOperationException(
                        $"Created type '{targetType.Name}' did not preserve source layer "
                        + $"{index + 1}'s thickness, material, and function.");
                }
            }
        }

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
