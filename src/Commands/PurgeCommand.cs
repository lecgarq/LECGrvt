using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Views;
using LECG.ViewModels;

namespace LECG.Commands
{
    /// <summary>
    /// Command to purge unused elements from the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PurgeCommand : RevitCommand
    {
        // We handle the transaction internally due to conditional logic
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            // 1. Load Settings
            var loadedSettings = SettingsManager.Load<PurgeViewModel>("PurgeSettings.json");
            var settings = ServiceLocator.GetRequiredService<PurgeViewModel>();
            settings.PurgeLineStyles = loadedSettings.PurgeLineStyles;
            settings.PurgeFillPatterns = loadedSettings.PurgeFillPatterns;
            settings.PurgeMaterials = loadedSettings.PurgeMaterials;
            settings.PurgeLevels = loadedSettings.PurgeLevels;

            // 2. Show options dialog
            PurgeView view = ServiceLocator.CreateWith<PurgeView>(settings);
            if (view.ShowDialog() != true) return;

            // 3. Save Settings
            SettingsManager.Save(settings, "PurgeSettings.json");

            // Check if anything selected
            if (!settings.PurgeLineStyles && !settings.PurgeFillPatterns && !settings.PurgeMaterials) return;

            // Show log window
            ShowLogWindow("Purge Unused");

            Log("Purge Unused Elements");
            Log("=====================");
            Log("");

            var purgeService = ServiceLocator.GetRequiredService<IPurgeService>();
            
            purgeService.PurgeAll(
                doc, 
                settings.PurgeLineStyles, 
                settings.PurgeFillPatterns, 
                settings.PurgeMaterials, 
                settings.PurgeLevels, 
                Log, 
                UpdateProgress);
        }
    }
}
