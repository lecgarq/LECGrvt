using Autodesk.Revit.DB;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class SynchronizeWithCentralService : ISynchronizeWithCentralService
    {
        public void Sync(Document doc, BatchJob job)
        {
            var relinquishOptions = new RelinquishOptions(true);
            var swcOptions = new SynchronizeWithCentralOptions();
            swcOptions.SetRelinquishOptions(relinquishOptions);
            swcOptions.Comment = $"LECG Batch Job {job.JobId}";
            swcOptions.SaveLocalBefore = false;
            swcOptions.SaveLocalAfter = false;

            Logger.Instance.Log($"[Batch] Synchronizing with central: {job.DisplayName}");
            doc.SynchronizeWithCentral(new TransactWithCentralOptions(), swcOptions);
            Logger.Instance.Log($"[Batch] Sync complete: {job.DisplayName}");
        }
    }
}
