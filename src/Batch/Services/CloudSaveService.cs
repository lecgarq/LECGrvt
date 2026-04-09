using Autodesk.Revit.DB;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class CloudSaveService : ICloudSaveService
    {
        public void Save(Document doc)
        {
            Logger.Instance.Log("[Batch] Saving non-workshared cloud model.");
            doc.Save();
            Logger.Instance.Log("[Batch] Cloud save complete.");
        }
    }
}
