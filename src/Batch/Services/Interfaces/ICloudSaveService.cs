using Autodesk.Revit.DB;
using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface ICloudSaveService
    {
        void Save(Document doc);
    }
}
