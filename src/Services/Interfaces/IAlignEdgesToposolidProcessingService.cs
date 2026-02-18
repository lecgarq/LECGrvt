using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesToposolidProcessingService
    {
        void Process(Document doc, Reference target, ReferenceIntersector intersector);
    }
}
