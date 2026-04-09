using Autodesk.Revit.DB;
using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface ISynchronizeWithCentralService
    {
        void Sync(Document doc, BatchJob job);
    }
}
