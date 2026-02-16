using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Views;
using LECG.Services;
using LECG.ViewModels;

namespace LECG.Commands
{
    /// <summary>
    /// Command to beautify the current view with optimal visual settings.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SexyRevitCommand : RevitCommand
    {
        // Service handles logic, no main transaction needed
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            View view = doc.ActiveView;
            if (view == null) throw new Exception("No active view.");

            // 1. Load Settings
            var settings = SettingsManager.Load<SexyRevitViewModel>("SexyRevitSettings.json");

            // 2. Show Dialog
            SexyRevitView dialog = new SexyRevitView(settings);
            if (dialog.ShowDialog() != true) return;

            // 3. Save Settings
            SettingsManager.Save(settings, "SexyRevitSettings.json");

            // 4. Run Service
            ShowLogWindow("Sexy Revit ✨");
                
            Log("Sexy Revit - View Beautification");
            Log("=================================");
            Log($"View: {view.Name}");
            Log("");

            var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
            service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);

            UpdateProgress(100, "Complete!");
            Log("");
            Log("=== COMPLETE ===");
            Log("✨ Your view is now sexy!");
        }
    }
}
