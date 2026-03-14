using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public sealed class PurgeContext
    {
        private PurgeContext(
            IReadOnlyList<Level> levels,
            HashSet<ElementId> parameterReferencedIds,
            HashSet<ElementId> usedLineStyleIds,
            HashSet<ElementId> usedFillPatternIds,
            HashSet<ElementId> usedMaterialIds,
            HashSet<ElementId> placedLevelIds)
        {
            Levels = levels;
            ParameterReferencedIds = parameterReferencedIds;
            UsedLineStyleIds = usedLineStyleIds;
            UsedFillPatternIds = usedFillPatternIds;
            UsedMaterialIds = usedMaterialIds;
            PlacedLevelIds = placedLevelIds;
        }

        public IReadOnlyList<Level> Levels { get; }
        public HashSet<ElementId> ParameterReferencedIds { get; }
        public HashSet<ElementId> UsedLineStyleIds { get; }
        public HashSet<ElementId> UsedFillPatternIds { get; }
        public HashSet<ElementId> UsedMaterialIds { get; }
        public HashSet<ElementId> PlacedLevelIds { get; }

        public static PurgeContext Create(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            var parameterReferencedIds = new HashSet<ElementId>();
            var usedLineStyleIds = new HashSet<ElementId>();
            var usedFillPatternIds = new HashSet<ElementId>();
            var usedMaterialIds = new HashSet<ElementId>();
            var placedLevelIds = new HashSet<ElementId>();

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                CollectParameterReferences(element, parameterReferencedIds);
                CollectMaterialIds(element, usedMaterialIds);
                CollectLineStyleId(element, usedLineStyleIds);
                CollectLevelId(element, placedLevelIds);
            }

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsElementType())
            {
                CollectParameterReferences(element, parameterReferencedIds);
                CollectMaterialIds(element, usedMaterialIds);
            }

            foreach (Material material in new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>())
            {
                AddIfValid(usedFillPatternIds, material.SurfaceForegroundPatternId);
                AddIfValid(usedFillPatternIds, material.SurfaceBackgroundPatternId);
                AddIfValid(usedFillPatternIds, material.CutForegroundPatternId);
                AddIfValid(usedFillPatternIds, material.CutBackgroundPatternId);
            }

            foreach (FilledRegionType filledRegionType in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>())
            {
                AddIfValid(usedFillPatternIds, filledRegionType.ForegroundPatternId);
                AddIfValid(usedFillPatternIds, filledRegionType.BackgroundPatternId);
            }

            IReadOnlyList<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            return new PurgeContext(
                levels,
                parameterReferencedIds,
                usedLineStyleIds,
                usedFillPatternIds,
                usedMaterialIds,
                placedLevelIds);
        }

        private static void AddIfValid(HashSet<ElementId> ids, ElementId id)
        {
            if (id != ElementId.InvalidElementId)
            {
                ids.Add(id);
            }
        }

        private static void CollectParameterReferences(Element element, HashSet<ElementId> parameterReferencedIds)
        {
            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    if (parameter.StorageType != StorageType.ElementId)
                    {
                        continue;
                    }

                    AddIfValid(parameterReferencedIds, parameter.AsElementId());
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect parameter references for element {element.Id}: {ex.Message}");
            }
        }

        private static void CollectMaterialIds(Element element, HashSet<ElementId> usedMaterialIds)
        {
            try
            {
                foreach (ElementId materialId in element.GetMaterialIds(false))
                {
                    AddIfValid(usedMaterialIds, materialId);
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect material references for element {element.Id}: {ex.Message}");
            }
        }

        private static void CollectLineStyleId(Element element, HashSet<ElementId> usedLineStyleIds)
        {
            if (element is not CurveElement curveElement)
            {
                return;
            }

            try
            {
                if (curveElement.LineStyle is GraphicsStyle graphicsStyle && graphicsStyle.GraphicsStyleCategory != null)
                {
                    AddIfValid(usedLineStyleIds, graphicsStyle.GraphicsStyleCategory.Id);
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect line style for curve {element.Id}: {ex.Message}");
            }
        }

        private static void CollectLevelId(Element element, HashSet<ElementId> placedLevelIds)
        {
            try
            {
                AddIfValid(placedLevelIds, element.LevelId);
            }
            catch (Exception ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect level for element {element.Id}: {ex.Message}");
            }
        }
    }
}
