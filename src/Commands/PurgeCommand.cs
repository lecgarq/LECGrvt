using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using LECG.Views.Base;
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
                out bool purgeLinePatterns,
                out bool purgeFillPatterns,
                out bool purgeMaterials,
                out bool purgeLevels,
                out bool purgeParameters,
                out int passCount,
                out bool cancelled))
            {
                if (cancelled) return;
            }
            else
            {
                if (cancelled) return;

                if (!TryGetOptionsFromFallbackDialog(
                    out purgeLineStyles,
                    out purgeLinePatterns,
                    out purgeFillPatterns,
                    out purgeMaterials,
                    out purgeLevels,
                    out purgeParameters,
                    out passCount))
                {
                    return;
                }
            }

            var purgeService = ServiceLocator.GetRequiredService<IPurgeService>();
            var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);

            ShowLogWindow("Purge Unused");
            purgeService.PurgeAll(doc, passCount, purgeLineStyles, purgeLinePatterns, purgeFillPatterns, purgeMaterials, purgeLevels, purgeParameters, reporter);
            Log("Purge Complete.");
        }

        private bool TryGetOptionsFromCustomDialog(
            out bool purgeLineStyles,
            out bool purgeLinePatterns,
            out bool purgeFillPatterns,
            out bool purgeMaterials,
            out bool purgeLevels,
            out bool purgeParameters,
            out int passCount,
            out bool cancelled)
        {
            purgeLineStyles = true;
            purgeLinePatterns = true;
            purgeFillPatterns = true;
            purgeMaterials = true;
            purgeLevels = false;
            purgeParameters = false;
            passCount = 1;
            cancelled = false;

            try
            {
                var loaded = SettingsManager.Load<PurgeDialogSettings>(PurgeSettingsFile) ?? new PurgeDialogSettings();
                var settings = ServiceLocator.GetRequiredService<PurgeViewModel>();
                settings.PurgeLineStyles = loaded.PurgeLineStyles;
                settings.PurgeLinePatterns = loaded.PurgeLinePatterns;
                settings.PurgeFillPatterns = loaded.PurgeFillPatterns;
                settings.PurgeMaterials = loaded.PurgeMaterials;
                settings.PurgeLevels = loaded.PurgeLevels;
                settings.PurgeParameters = loaded.PurgeParameters;
                settings.IsDeepPurge = loaded.IsDeepPurge;

                var view = ServiceLocator.GetRequiredService<PurgeView>();
                view.DataContext = settings; 
                bool? result = view.ShowDialog();
                if (result != true)
                {
                    cancelled = true;
                    return false;
                }

                SettingsManager.Save(new PurgeDialogSettings
                {
                    PurgeLineStyles = settings.PurgeLineStyles,
                    PurgeLinePatterns = settings.PurgeLinePatterns,
                    PurgeFillPatterns = settings.PurgeFillPatterns,
                    PurgeMaterials = settings.PurgeMaterials,
                    PurgeLevels = settings.PurgeLevels,
                    PurgeParameters = settings.PurgeParameters,
                    IsDeepPurge = settings.IsDeepPurge
                }, PurgeSettingsFile);

                purgeLineStyles = settings.PurgeLineStyles;
                purgeLinePatterns = settings.PurgeLinePatterns;
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
            out bool purgeLinePatterns,
            out bool purgeFillPatterns,
            out bool purgeMaterials,
            out bool purgeLevels,
            out bool purgeParameters,
            out int passCount)
        {
            purgeLineStyles = true;
            purgeLinePatterns = true;
            purgeFillPatterns = true;
            purgeMaterials = true;
            purgeLevels = false;
            purgeParameters = false;
            passCount = 1;

            int selected = LecgDialog.ShowOptions(
                "LECG Purge Unused",
                "Choose purge mode. Safe mode avoids family-parameter purge.",
                "Safe Purge (Line Styles, Line Patterns, Fill Patterns, Materials)",
                "Deep Purge (+Levels, 3 passes)");

            if (selected < 0) return false;

            purgeLevels = selected == 1;
            passCount = selected == 1 ? 3 : 1;
            return true;
        }
    }
}
