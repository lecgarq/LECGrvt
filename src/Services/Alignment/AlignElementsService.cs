using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignElementsService
    {
        private readonly ITransactionService _transactionService;

        public AlignElementsService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public void Align(Document doc, Element reference, List<Element> targets, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (reference == null || targets == null || !targets.Any()) return;

            _transactionService.Run(doc, $"Align {mode}", currentDoc =>
            {
                // Get Reference BoundingBox
                BoundingBoxXYZ refBox = reference.get_BoundingBox(currentDoc.ActiveView);
                if (refBox == null) return;

                foreach (Element target in targets)
                {
                    BoundingBoxXYZ targetBox = target.get_BoundingBox(currentDoc.ActiveView);
                    if (targetBox == null) continue;

                    XYZ translation = CalculateTranslation(refBox, targetBox, mode);

                    if (!translation.IsZeroLength())
                    {
                        ElementTransformUtils.MoveElement(currentDoc, target.Id, translation);
                    }
                }
            });
        }

        public void Distribute(Document doc, List<Element> elements, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (elements == null || elements.Count < 3) return; // Need at least 3 items to distribute meaningfully

            _transactionService.Run(doc, $"Distribute {mode}", currentDoc =>
            {
                List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = BuildAndSort(currentDoc, elements, mode);

                if (sortedItems.Count < 3) return;

                MoveIntermediateElements(currentDoc, sortedItems, mode);
            });
        }

        private static XYZ CalculateTranslation(BoundingBoxXYZ referenceBox, BoundingBoxXYZ targetBox, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(referenceBox);
            ArgumentNullException.ThrowIfNull(targetBox);

            switch (mode)
            {
                case AlignMode.Left:
                    return new XYZ(referenceBox.Min.X - targetBox.Min.X, 0, 0);
                case AlignMode.Center:
                    double refCenter = (referenceBox.Min.X + referenceBox.Max.X) / 2.0;
                    double targetCenter = (targetBox.Min.X + targetBox.Max.X) / 2.0;
                    return new XYZ(refCenter - targetCenter, 0, 0);
                case AlignMode.Right:
                    return new XYZ(referenceBox.Max.X - targetBox.Max.X, 0, 0);
                case AlignMode.Top:
                    return new XYZ(0, referenceBox.Max.Y - targetBox.Max.Y, 0);
                case AlignMode.Middle:
                    double refMid = (referenceBox.Min.Y + referenceBox.Max.Y) / 2.0;
                    double targetMid = (targetBox.Min.Y + targetBox.Max.Y) / 2.0;
                    return new XYZ(0, refMid - targetMid, 0);
                case AlignMode.Bottom:
                    return new XYZ(0, referenceBox.Min.Y - targetBox.Min.Y, 0);
                default:
                    return XYZ.Zero;
            }
        }

        private static List<(Element Element, BoundingBoxXYZ Box, double Position)> BuildAndSort(
            Document doc,
            List<Element> elements,
            AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);

            List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = new List<(Element, BoundingBoxXYZ, double)>();

            foreach (Element el in elements)
            {
                BoundingBoxXYZ? box = el.get_BoundingBox(doc.ActiveView);
                if (box == null) continue;

                double pos = (mode == AlignMode.DistributeHorizontally)
                    ? (box.Min.X + box.Max.X) / 2.0
                    : (box.Min.Y + box.Max.Y) / 2.0;

                sortedItems.Add((el, box, pos));
            }

            return sortedItems.OrderBy(x => x.Position).ToList();
        }

        private static void MoveIntermediateElements(
            Document doc,
            List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems,
            AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(sortedItems);
            if (sortedItems.Count < 3)
            {
                return;
            }

            var first = sortedItems[0];
            var last = sortedItems[sortedItems.Count - 1];

            double totalDistance = last.Position - first.Position;
            double step = totalDistance / (sortedItems.Count - 1);

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
        }
    }
}
