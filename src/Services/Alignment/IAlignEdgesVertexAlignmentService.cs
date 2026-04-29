using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Clipper2Lib;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesVertexAlignmentService
    {
        (int movedCount, int skippedCount, int missCount) AlignVertices(SlabShapeEditor editor, ReferenceIntersector intersector, PathsD? overlapRegion = null);
    }
}
