using Autodesk.Revit.DB;

namespace LECG.Services
{
    public readonly record struct AlignEdgesHitInfo(XYZ Point, ElementId ElementId);
}
