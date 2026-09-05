using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IBatchJobRoutine
    {
        string Name { get; }
        string Description { get; }
        void Execute(UIApplication app, Document doc, BatchJob job);
    }
}
