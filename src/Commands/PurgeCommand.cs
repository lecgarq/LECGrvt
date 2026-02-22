using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System;

namespace LECG.Commands
{
    /// <summary>
    /// Command to purge unused elements from the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PurgeCommand : RevitCommand
    {
        private const string PurgeSettingsFile = "PurgeDialogSettings.json";

        // We handle the transaction internally due to conditional logic
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            UIApplication app = uiDoc.Application;

            if (TryGetOptionsFromCustomDialog(
                out bool purgeLineStyles,
                out bool purgeFillPatterns,
                out bool purgeMaterials,
                out bool purgeLevels,
                out bool purgeParameters,
                out int passCount,
                out bool cancelled))
            {
                if (cancelled) return;

                var purgeService = ServiceLocator.GetRequiredService<IPurgeService>();
                
                ShowLogWindow("Purge Unused");
                purgeService.PurgeAll(doc, passCount, purgeLineStyles, purgeFillPatterns, purgeMaterials, purgeLevels, purgeParameters, Log, UpdateProgress);
                Log("Purge Complete.");
            }
        }

        private bool TryGetOptionsFromCustomDialog(
            out bool purgeLineStyles,
            out bool purgeFillPatterns,
            out bool purgeMaterials,
            out bool purgeLevels,
            out bool purgeParameters,
            out int passCount,
            out bool cancelled)
        {
            purgeLineStyles = true;
            purgeFillPatterns = true;
            purgeMaterials = true;
            purgeLevels = false;
            purgeParameters = false;
            passCount = 1;
            cancelled = false;

            try
            {
                var loaded = SettingsManager.Load<PurgeDialogSettings>(PurgeSettingsFile) ?? new PurgeDialogSettings();
                var settings = new PurgeViewModel();

                settings.PurgeLineStyles = loaded.PurgeLineStyles;
                settings.PurgeFillPatterns = loaded.PurgeFillPatterns;
                settings.PurgeMaterials = loaded.PurgeMaterials;
                settings.PurgeLevels = loaded.PurgeLevels;
                settings.PurgeParameters = loaded.PurgeParameters;
                settings.IsDeepPurge = loaded.IsDeepPurge;

                var view = new PurgeView(settings);
                bool? result = view.ShowDialog();
                if (result != true)
                {
                    cancelled = true;
                    return false;
                }

                SettingsManager.Save(new PurgeDialogSettings
                {
                    PurgeLineStyles = settings.PurgeLineStyles,
                    PurgeFillPatterns = settings.PurgeFillPatterns,
                    PurgeMaterials = settings.PurgeMaterials,
                    PurgeLevels = settings.PurgeLevels,
                    PurgeParameters = settings.PurgeParameters,
                    IsDeepPurge = settings.IsDeepPurge
                }, PurgeSettingsFile);

                purgeLineStyles = settings.PurgeLineStyles;
                purgeFillPatterns = settings.PurgeFillPatterns;
                purgeMaterials = settings.PurgeMaterials;
                purgeLevels = settings.PurgeLevels;
                purgeParameters = settings.PurgeParameters;
                passCount = settings.IsDeepPurge ? 3 : 1;
                return true;
            }
            catch (Exception ex)
            {
                Log($"Purge WPF dialog failed. Using native fallback. {ex.Message}");
                return false;
            }
        }

        private static bool TryGetOptionsFromFallbackDialog(
            out bool purgeLineStyles,
            out bool purgeFillPatterns,
            out bool purgeMaterials,
            out bool purgeLevels,
            out bool purgeParameters,
            out int passCount)
        {
            purgeLineStyles = true;
            purgeFillPatterns = true;
            purgeMaterials = true;
            purgeLevels = false;
            purgeParameters = false;
            passCount = 1;

            var dialog = new TaskDialog("LECG Purge Unused")
            {
                MainInstruction = "Choose purge mode",
                MainContent = "Safe mode avoids family-parameter purge.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Safe Purge (Line Styles, Fill Patterns, Materials)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Deep Purge (+Levels, 3 passes)");

            TaskDialogResult dialogResult = dialog.Show();
            if (dialogResult != TaskDialogResult.CommandLink1 && dialogResult != TaskDialogResult.CommandLink2) return false;

            purgeLevels = dialogResult == TaskDialogResult.CommandLink2;
            passCount = dialogResult == TaskDialogResult.CommandLink2 ? 3 : 1;
            return true;
        }
    }
}
