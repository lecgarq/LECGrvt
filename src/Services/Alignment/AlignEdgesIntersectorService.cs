using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesIntersectorService
    {
        public ReferenceIntersector Create(Document doc, IList<Reference> references)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(references);

            var refElementIds = new List<ElementId>();

            foreach (Reference r in references)
            {
                Element? elem = doc.GetElement(r);
                if (elem == null) continue;

                refElementIds.Add(elem.Id);
            }

            if (refElementIds.Count == 0)
            {
                throw new InvalidOperationException("No valid reference element IDs were found.");
            }

            View3D view3D = FindView3D(doc);
            var intersector = new ReferenceIntersector(refElementIds, FindReferenceTarget.Face, view3D)
            {
                FindReferencesInRevitLinks = true
            };
            return intersector;
        }

        public ReferenceIntersector CreateBroad(Document doc, ICollection<ElementId> excludeIds)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(excludeIds);

            var targetIds = new List<ElementId>();

            // All RevitLinkInstances (linked models)
            var linkIds = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .ToElementIds();
            targetIds.AddRange(linkIds);

            // All Floors (excluding targets)
            var floorIds = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .ToElementIds()
                .Where(id => !excludeIds.Contains(id));
            targetIds.AddRange(floorIds);

            // All Toposolids (excluding targets)
            var topoIds = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Toposolid)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Where(id => !excludeIds.Contains(id));
            targetIds.AddRange(topoIds);

            if (targetIds.Count == 0)
            {
                throw new InvalidOperationException("No reference geometry found. Ensure linked models or surface elements exist in the project.");
            }

            View3D view3D = FindView3D(doc);
            var intersector = new ReferenceIntersector(targetIds, FindReferenceTarget.Face, view3D)
            {
                FindReferencesInRevitLinks = true
            };
            return intersector;
        }

        private static View3D FindView3D(Document doc)
        {
            // Prefer the active view if it's a usable 3D view
            if (doc.ActiveView is View3D active && !active.IsTemplate)
            {
                return active;
            }

            // Fall back to any non-template 3D view (prefer one without section box)
            View3D? view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.IsSectionBoxActive ? 1 : 0)
                .FirstOrDefault();

            if (view3D == null)
            {
                throw new InvalidOperationException("No suitable 3D view found for ray tracing.");
            }

            return view3D;
        }
    }
}
