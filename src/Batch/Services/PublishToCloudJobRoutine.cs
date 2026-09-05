using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    /// <summary>
    /// Prepares a cloud Revit model for ACC publishing:
    ///   1. Creates or reuses the 3D view "HMO_LECG_3D-DEFAULT"
    ///   2. Disables all Annotation categories in that view's VG overrides
    ///   3. Creates/overwrites the ViewSheetSet "ACC_PUBLISH_SET" containing only that view
    ///   4. Sets the view as Starting View
    ///   5. Sets the view as Preview View
    /// After Execute() returns the handler saves (SaveCloudModel) and publishes.
    /// </summary>
    public class PublishToCloudJobRoutine : IBatchJobRoutine
    {
        private const string ViewName = "HMO_LECG_3D-DEFAULT";
        private const string PublishSetName = "ACC_PUBLISH_SET";

        public string Name => "Publish To Cloud";
        public string Description => "Creates view HMO_LECG_3D-DEFAULT, disables annotation categories, " +
                                     "sets ViewSheetSet ACC_PUBLISH_SET as the publish set, " +
                                     "then sets starting and preview view before saving to ACC.";

        public void Execute(UIApplication app, Document doc, BatchJob job)
        {
            string modelName = job.DisplayName;
            Logger.Instance.Log($"[{modelName}] Starting Publish To Cloud routine.");

            using Transaction tx = new Transaction(doc, "HMO LECG - Prepare Publish Settings");
            tx.Start();
            try
            {
                // Step 1: Create or reuse the named 3D view
                View3D publishView = EnsurePublishView(doc, modelName);

                // Step 2: Disable annotation categories in VG
                DisableAnnotationCategories(doc, publishView, modelName);

                // Step 3: Create / overwrite the ViewSheetSet
                EnsurePublishSet(doc, publishView, modelName);

                // Step 4: Set as Starting View
                SetStartingView(doc, publishView, modelName);

                // Step 5: Set as Preview View
                SetPreviewView(doc, publishView, modelName);

                tx.Commit();
                Logger.Instance.LogSuccess($"[{modelName}] Publish preparation complete.");
            }
            catch
            {
                try { tx.RollBack(); } catch { }
                throw;
            }
        }

        // ── Step 1 ────────────────────────────────────────────────────────────

        private static View3D EnsurePublishView(Document doc, string modelName)
        {
            // Reuse existing view if present
            foreach (View3D v in new FilteredElementCollector(doc).OfClass(typeof(View3D)))
            {
                if (!v.IsTemplate && v.Name == ViewName)
                {
                    ConfigureView(v);
                    Logger.Instance.Log($"[{modelName}] Reusing existing view '{ViewName}'.");
                    return v;
                }
            }

            // Create new isometric 3D view
            ElementId vftId = GetThreeDViewFamilyTypeId(doc);
            View3D newView = View3D.CreateIsometric(doc, vftId);
            newView.Name = ViewName;
            ConfigureView(newView);
            Logger.Instance.Log($"[{modelName}] Created view '{ViewName}'.");
            return newView;
        }

        private static ElementId GetThreeDViewFamilyTypeId(Document doc)
        {
            foreach (ViewFamilyType vft in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)))
            {
                if (vft.ViewFamily == ViewFamily.ThreeDimensional)
                    return vft.Id;
            }
            throw new InvalidOperationException("No 3D ViewFamilyType found in document.");
        }

        private static void ConfigureView(View3D view)
        {
            try { view.CropBoxActive = false; } catch { }
            try { view.CropBoxVisible = false; } catch { }
            try { view.DisplayStyle = DisplayStyle.ShadingWithEdges; } catch { }
        }

        // ── Step 2 ────────────────────────────────────────────────────────────

        private static void DisableAnnotationCategories(Document doc, View3D view, string modelName)
        {
            int count = 0;
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.CategoryType != CategoryType.Annotation)
                    continue;
                if (!view.CanCategoryBeHidden(cat.Id))
                    continue;
                try
                {
                    view.SetCategoryHidden(cat.Id, true);
                    count++;
                }
                catch { }
            }
            Logger.Instance.Log($"[{modelName}] Disabled {count} annotation categories in VG.");
        }

        // ── Step 3 ────────────────────────────────────────────────────────────

        private static void EnsurePublishSet(Document doc, View3D publishView, string modelName)
        {
            PrintManager pm = doc.PrintManager;
            pm.PrintRange = PrintRange.Select;
            pm.Apply();

            ViewSet viewSet = new ViewSet();
            viewSet.Insert(publishView);

            ViewSheetSetting vss = pm.ViewSheetSetting;
            vss.CurrentViewSheetSet.Views = viewSet;

            bool saved = vss.SaveAs(PublishSetName);
            if (!saved)
                Logger.Instance.LogWarning($"[{modelName}] ViewSheetSetting.SaveAs('{PublishSetName}') returned false — set may already exist and was updated.");
            else
                Logger.Instance.Log($"[{modelName}] Created ViewSheetSet '{PublishSetName}'.");
        }

        // ── Step 4 ────────────────────────────────────────────────────────────

        private static void SetStartingView(Document doc, View3D view, string modelName)
        {
            StartingViewSettings? settings = StartingViewSettings.GetStartingViewSettings(doc);
            if (settings == null)
            {
                Logger.Instance.LogWarning($"[{modelName}] Could not retrieve StartingViewSettings.");
                return;
            }

            if (!settings.IsAcceptableStartingView(view.Id))
            {
                Logger.Instance.LogWarning($"[{modelName}] View '{view.Name}' is not acceptable as a starting view.");
                return;
            }

            settings.ViewId = view.Id;
            Logger.Instance.Log($"[{modelName}] Starting view set to '{view.Name}'.");
        }

        // ── Step 5 ────────────────────────────────────────────────────────────

        private static void SetPreviewView(Document doc, View3D view, string modelName)
        {
            DocumentPreviewSettings? preview = null;
            try
            {
                preview = doc.GetDocumentPreviewSettings();
                if (preview == null)
                {
                    Logger.Instance.LogWarning($"[{modelName}] Could not retrieve DocumentPreviewSettings.");
                    return;
                }

                if (!preview.IsViewIdValidForPreview(view.Id))
                {
                    Logger.Instance.LogWarning($"[{modelName}] View '{view.Name}' is not valid for preview.");
                    return;
                }

                preview.PreviewViewId = view.Id;
                try { preview.ForceViewUpdate(true); } catch { }
                Logger.Instance.Log($"[{modelName}] Preview view set to '{view.Name}'.");
            }
            finally
            {
                try { preview?.Dispose(); } catch { }
            }
        }
    }
}
