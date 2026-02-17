#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignElementsService : IAlignElementsService
    {
        private readonly IAlignElementsTranslationService _translationService;

        public AlignElementsService() : this(new AlignElementsTranslationService())
        {
        }

        public AlignElementsService(IAlignElementsTranslationService translationService)
        {
            _translationService = translationService;
        }

        public void Align(Document doc, Element reference, List<Element> targets, AlignMode mode)
        {
            if (reference == null || targets == null || !targets.Any()) return;

            using (Transaction t = new Transaction(doc, $"Align {mode}"))
            {
                t.Start();

                // Get Reference BoundingBox
                BoundingBoxXYZ refBox = reference.get_BoundingBox(doc.ActiveView);
                if (refBox == null) return;

                foreach (Element target in targets)
                {
                    BoundingBoxXYZ targetBox = target.get_BoundingBox(doc.ActiveView);
                    if (targetBox == null) continue;

                    XYZ translation = _translationService.Calculate(refBox, targetBox, mode);

                    if (!translation.IsZeroLength())
                    {
                        ElementTransformUtils.MoveElement(doc, target.Id, translation);
                    }
                }

                t.Commit();
            }
        }

        public void Distribute(Document doc, List<Element> elements, AlignMode mode)
        {
            if (elements == null || elements.Count < 3) return; // Need at least 3 items to distribute meaningfully

            using (Transaction t = new Transaction(doc, $"Distribute {mode}"))
            {
                t.Start();

                List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = new List<(Element, BoundingBoxXYZ, double)>();

                // 1. Get Boxes and Sort
                foreach (var el in elements)
                {
                    var box = el.get_BoundingBox(doc.ActiveView);
                    if (box == null) continue;

                    double pos = (mode == AlignMode.DistributeHorizontally) 
                        ? (box.Min.X + box.Max.X) / 2.0 
                        : (box.Min.Y + box.Max.Y) / 2.0;

                    sortedItems.Add((el, box, pos));
                }

                // Sort by position
                sortedItems = sortedItems.OrderBy(x => x.Position).ToList();

                if (sortedItems.Count < 3) return;

                // 2. Calculate Spacing
                var first = sortedItems.First();
                var last = sortedItems.Last();

                double totalDistance = last.Position - first.Position;
                double step = totalDistance / (sortedItems.Count - 1);

                // 3. Move intermediates
                for (int i = 1; i < sortedItems.Count - 1; i++)
                {
                    var item = sortedItems[i];
                    double targetPos = first.Position + (step * i);
                    double currentPos = item.Position;
                    double diff = targetPos - currentPos;

                    XYZ translation = (mode == AlignMode.DistributeHorizontally)
                        ? new XYZ(diff, 0, 0)
                        : new XYZ(0, diff, 0);

                    if (!translation.IsZeroLength())
                    {
                        ElementTransformUtils.MoveElement(doc, item.Element.Id, translation);
                    }
                }

                t.Commit();
            }
        }
    }
}
