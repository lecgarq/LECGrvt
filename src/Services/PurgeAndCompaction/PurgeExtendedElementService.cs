using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitExceptions = Autodesk.Revit.Exceptions;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeExtendedElementService
    {
        private readonly PurgeDeleteElementService _purgeDeleteElementService;

        public PurgeExtendedElementService(PurgeDeleteElementService purgeDeleteElementService)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedGroups(Document doc, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            logCallback?.Invoke("Scanning for unused group types...");

            HashSet<ElementId> usedTypeIds = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Select(group => group.GetTypeId())
                .Where(id => id != ElementId.InvalidElementId)
                .ToHashSet();

            // Attached detail group types belong to a model group type and have no placed Group
            // instance until shown in a view — treat them as used when their parent type is used.
            foreach (ElementId usedTypeId in usedTypeIds.ToList())
            {
                if (doc.GetElement(usedTypeId) is not GroupType usedType) continue;

                try
                {
                    usedTypeIds.UnionWith(usedType.GetAvailableAttachedDetailGroupTypeIds());
                }
                catch (Exception ex) when (IsExpectedRevitException(ex))
                {
                    // Detail group types themselves have no attached detail groups.
                }
            }

            return DeleteElements(
                new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Cast<GroupType>()
                    .Where(groupType => !usedTypeIds.Contains(groupType.Id))
                    .Select(groupType => (groupType.Id, groupType.Name)),
                doc,
                logCallback,
                "group types");
        }

        public int PurgeUnusedGridTypes(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused grid types...");

            HashSet<ElementId> usedTypeIds = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(grid => grid.GetTypeId())
                .Where(id => id != ElementId.InvalidElementId)
                .ToHashSet();

            // Multi-segment grids reference a GridType but are not Grid subclasses; deleting a
            // type they use would cascade-delete the multi-segment grid itself.
            foreach (Element multiSegmentGrid in new FilteredElementCollector(doc).OfClass(typeof(MultiSegmentGrid)))
            {
                ElementId typeId = multiSegmentGrid.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    usedTypeIds.Add(typeId);
                }
            }

            return DeleteElements(
                new FilteredElementCollector(doc).OfClass(typeof(GridType)).Cast<GridType>()
                    .Where(gridType => !usedTypeIds.Contains(gridType.Id))
                    .Select(gridType => (gridType.Id, gridType.Name)),
                doc,
                logCallback,
                "grid types");
        }

        public int PurgeUnusedLevelTypes(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused level types...");

            HashSet<ElementId> usedTypeIds = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(level => level.GetTypeId())
                .Where(id => id != ElementId.InvalidElementId)
                .ToHashSet();

            return DeleteElements(
                new FilteredElementCollector(doc).OfClass(typeof(LevelType)).Cast<LevelType>()
                    .Where(levelType => !usedTypeIds.Contains(levelType.Id))
                    .Select(levelType => (levelType.Id, levelType.Name)),
                doc,
                logCallback,
                "level types");
        }

        public int PurgeConstraints(Document doc, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            logCallback?.Invoke("Scanning for constraints...");

            // Constraints are special: system/alignment constraints often cannot be deleted
            // by Revit. We delete what we can and silently skip the rest to avoid log noise.
            List<(ElementId id, string name)> constraints = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Constraints)
                .WhereElementIsNotElementType()
                .Select(element => (element.Id, element.Name))
                .ToList();

            int deleted = 0;
            foreach ((ElementId id, string name) in constraints)
            {
                try
                {
                    if (id.Value <= 0) continue;
                    Element? element = doc.GetElement(id);
                    if (element == null || !element.IsValidObject) continue;

                    doc.Delete(id);
                    deleted++;
                }
                catch (Exception ex) when (IsExpectedRevitException(ex))
                {
                    // System/alignment constraints that Revit won't allow deleting are expected.
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} constraints.");
            return deleted;
        }

        public int PurgeUnplacedRooms(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unplaced rooms...");

            return DeleteElements(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType()
                    .OfType<Room>()
                    .Where(room => room.Location == null)
                    .Select(room => (room.Id, string.IsNullOrWhiteSpace(room.Name) ? room.Number : $"{room.Number} - {room.Name}")),
                doc,
                logCallback,
                "unplaced rooms");
        }

        public int PurgeUnusedViewTemplates(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused view templates...");

            HashSet<ElementId> usedTemplateIds = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(view => !view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId)
                .Select(view => view.ViewTemplateId)
                .ToHashSet();

            // Templates assigned as the default for a view type are in use even if no view
            // currently references them.
            foreach (ViewFamilyType viewFamilyType in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>())
            {
                if (viewFamilyType.DefaultTemplateId != ElementId.InvalidElementId)
                {
                    usedTemplateIds.Add(viewFamilyType.DefaultTemplateId);
                }
            }

            return DeleteElements(
                new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .Where(view => view.IsTemplate && !usedTemplateIds.Contains(view.Id))
                    .Select(view => (view.Id, view.Name)),
                doc,
                logCallback,
                "view templates");
        }

        public int PurgeUnusedViewFilters(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused view filters...");

            HashSet<ElementId> usedFilterIds = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .SelectMany(GetAppliedFilterIds)
                .ToHashSet();

            return DeleteElements(
                new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>()
                    .Where(filter => !usedFilterIds.Contains(filter.Id))
                    .Select(filter => (filter.Id, filter.Name)),
                doc,
                logCallback,
                "view filters");
        }

        private static IEnumerable<ElementId> GetAppliedFilterIds(View view)
        {
            try
            {
                return view.GetFilters();
            }
            catch (Exception ex) when (IsExpectedRevitException(ex))
            {
                return Array.Empty<ElementId>();
            }
        }

        // Revit's ArgumentException/InvalidOperationException derive from
        // Autodesk.Revit.Exceptions.ApplicationException, not their System namesakes,
        // so both families must be listed or Revit exceptions abort the purge pass.
        private static bool IsExpectedRevitException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private int DeleteElements(IEnumerable<(ElementId id, string name)> candidates, Document doc, Action<string>? logCallback, string categoryName)
        {
            // Materialize to avoid Revit iterator invalidation when deleting elements
            List<(ElementId id, string name)> candidateList = candidates.ToList();
            int deleted = 0;
            foreach ((ElementId id, string name) in candidateList)
            {
                if (_purgeDeleteElementService.DeleteElement(doc, id, name, logCallback))
                {
                    deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} {categoryName}.");
            return deleted;
        }
    }
}
