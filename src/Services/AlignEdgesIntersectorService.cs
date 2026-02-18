using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesIntersectorService : IAlignEdgesIntersectorService
    {
        public ReferenceIntersector Create(Document doc, IList<Reference> references)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(references);

            ICollection<ElementId> refElementIds = references
                .Select(r => doc.GetElement(r)?.Id)
                .Where(id => id != null)
                .ToList()!;

            if (refElementIds.Count == 0)
            {
                throw new InvalidOperationException("No valid reference element IDs were found.");
            }

            View3D? view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.IsSectionBoxActive == false);

            if (view3D == null)
            {
                if (doc.ActiveView is View3D v)
                {
                    view3D = v;
                }
                else
                {
                    throw new InvalidOperationException("No suitable 3D view found for ray tracing.");
                }
            }

            return new ReferenceIntersector(refElementIds, FindReferenceTarget.Element, view3D);
        }
    }
}
