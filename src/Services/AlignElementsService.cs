#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignElementsService : IAlignElementsService
    {
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

                    XYZ translation = XYZ.Zero;

                    switch (mode)
                    {
                        case AlignMode.Left:
                            translation = new XYZ(refBox.Min.X - targetBox.Min.X, 0, 0);
                            break;
                        case AlignMode.Center: // Horizontal Center
                            double refCenter = (refBox.Min.X + refBox.Max.X) / 2.0;
                            double targetCenter = (targetBox.Min.X + targetBox.Max.X) / 2.0;
                            translation = new XYZ(refCenter - targetCenter, 0, 0);
                            break;
                        case AlignMode.Right:
                            translation = new XYZ(refBox.Max.X - targetBox.Max.X, 0, 0);
                            break;
                        case AlignMode.Top:
                            translation = new XYZ(0, refBox.Max.Y - targetBox.Max.Y, 0);
                            break;
                        case AlignMode.Middle: // Vertical Middle
                             double refMid = (refBox.Min.Y + refBox.Max.Y) / 2.0;
                             double targetMid = (targetBox.Min.Y + targetBox.Max.Y) / 2.0;
                             translation = new XYZ(0, refMid - targetMid, 0);
                            break;
                        case AlignMode.Bottom:
                            translation = new XYZ(0, refBox.Min.Y - targetBox.Min.Y, 0);
                            break;
                    }

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
