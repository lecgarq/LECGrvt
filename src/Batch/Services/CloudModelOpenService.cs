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
            // Validate GUIDs before attempting cloud path construction.
            // ModelGuid may be absent for non-workshared ACC models created in Revit 2026+
            // where extension.data.revisionId is not populated.
            if (!Guid.TryParse(job.ModelGuid, out Guid parsedModelGuid))
            {
                Logger.Instance.LogWarning($"[Batch] Cannot open '{job.DisplayName}': ModelGuid is missing or invalid ('{job.ModelGuid}'). " +
                    "This model may be a non-workshared ACC file without a revisionId. " +
                    "Ensure the model is a cloud workshared (C4R) file or that it has a valid revisionId.");
                return null;
            }

            if (!Guid.TryParse(job.ProjectGuid, out Guid parsedProjectGuid))
            {
                Logger.Instance.LogWarning($"[Batch] Cannot open '{job.DisplayName}': ProjectGuid is invalid ('{job.ProjectGuid}').");
                return null;
            }

            // Check if already open — reuse to avoid double-open
            foreach (Document doc in app.Application.Documents)
            {
                if (doc.IsModelInCloud && IsMatchingCloudModel(doc, job))
                {
                    Logger.Instance.Log($"[Batch] Reusing open document: {job.DisplayName}");
                    return doc;
                }
            }

            Logger.Instance.Log($"[Batch] Opening {job.ModelType} cloud model: '{job.DisplayName}' (Project: {parsedProjectGuid}, Model: {parsedModelGuid}, Region: {job.Region})");

            ModelPath cloudPath;
            try
            {
                cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                    job.Region,
                    parsedProjectGuid,
                    parsedModelGuid);
            }
            catch (Exception ex) when (IsCentralModelMissing(ex))
            {
                string modelTypeInfo = job.ModelType == ModelType.CloudWorkshared ? "Cloud Workshared (C4R)" : "Cloud Non-Workshared (CNV)";
                Logger.Instance.LogError($"[Batch] FAILED - The model '{job.DisplayName}' is not a valid {modelTypeInfo} central model at the specified GUIDs. " +
                    "Ensure the model has been correctly initiated in the cloud from within Revit.");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"[Batch] FAILED to construct cloud path for '{job.DisplayName}': {ex.Message}");
                return null;
            }

            var openOptions = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DoNotDetach,
                Audit = false,
            };

            Logger.Instance.Log($"[Batch] OpenDocumentFile: {job.DisplayName}");
            return app.Application.OpenDocumentFile(cloudPath, openOptions);
        }

        private static bool IsMatchingCloudModel(Document doc, BatchJob job)
        {
            try
            {
                ModelPath? path = doc.GetCloudModelPath();
                if (path == null) return false;

                bool projectMatch = path.GetProjectGUID() == new Guid(job.ProjectGuid);
                bool modelMatch = path.GetModelGUID() == new Guid(job.ModelGuid);

                return projectMatch && modelMatch;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCentralModelMissing(Exception ex)
        {
            Type? exceptionType = ex.GetType();
            return string.Equals(exceptionType.FullName, "Autodesk.Revit.Exceptions.CentralModelMissingException", StringComparison.Ordinal)
                || string.Equals(exceptionType.Name, "CentralModelMissingException", StringComparison.Ordinal);
        }
    }
}
