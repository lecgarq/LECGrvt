using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class CloudModelOpenService : ICloudModelOpenService
    {
        public Document? Open(UIApplication app, BatchJob job)
        {
            // Check if already open — reuse to avoid double-open
            foreach (Document doc in app.Application.Documents)
            {
                if (doc.IsModelInCloud && IsMatchingCloudModel(doc, job))
                {
                    Logger.Instance.Log($"[Batch] Reusing open document: {job.DisplayName}");
                    return doc;
                }
            }

            ModelPath cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                job.Region,
                new Guid(job.ProjectGuid),
                new Guid(job.ModelGuid));

            var openOptions = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DoNotDetach,
                Audit = false,
            };

            Logger.Instance.Log($"[Batch] Opening cloud model: {job.DisplayName}");
            return app.Application.OpenDocumentFile(cloudPath, openOptions);
        }

        private static bool IsMatchingCloudModel(Document doc, BatchJob job)
        {
            try
            {
                ModelPath? path = doc.GetCloudModelPath();
                if (path == null) return false;
                return path.GetModelGUID() == new Guid(job.ModelGuid)
                    && path.GetProjectGUID() == new Guid(job.ProjectGuid);
            }
            catch
            {
                return false;
            }
        }
    }
}
