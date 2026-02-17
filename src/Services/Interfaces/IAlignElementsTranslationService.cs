using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignElementsTranslationService
    {
        XYZ Calculate(BoundingBoxXYZ referenceBox, BoundingBoxXYZ targetBox, AlignMode mode);
    }
}
