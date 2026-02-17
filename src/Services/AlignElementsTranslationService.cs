using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignElementsTranslationService : IAlignElementsTranslationService
    {
        public XYZ Calculate(BoundingBoxXYZ referenceBox, BoundingBoxXYZ targetBox, AlignMode mode)
        {
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
    }
}
