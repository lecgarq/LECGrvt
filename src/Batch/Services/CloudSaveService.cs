using Autodesk.Revit.DB;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class CloudSaveService : ICloudSaveService
    {
        public void Save(Document doc)
        {
            // Models opened directly from a cloud path must be saved back with SaveCloudModel().
            // Models opened from a local path use the regular Save().
            if (IsCloudModel(doc))
            {
                Logger.Instance.Log("[Batch] Saving cloud model (SaveCloudModel).");
                doc.SaveCloudModel();
                Logger.Instance.LogSuccess("[Batch] Cloud model saved.");
            }
            else
            {
                Logger.Instance.Log("[Batch] Saving local model.");
                doc.Save();
                Logger.Instance.LogSuccess("[Batch] Local save complete.");
            }
        }

        private static bool IsCloudModel(Document doc)
        {
            try
            {
                ModelPath? cloudPath = doc.GetCloudModelPath();
                return cloudPath != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
