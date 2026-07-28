using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    /// <summary>
    /// Command to beautify the current view with optimal visual settings.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SexyRevitCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            View view = doc.ActiveView;
            if (view == null) throw new InvalidOperationException("No active view.");

            // 1. Load Settings
            var loadedSettings = SettingsManager.Load<SexyRevitViewModel>("SexyRevitSettings.json");
            SexyRevitView dialog = ServiceLocator.GetRequiredService<SexyRevitView>();
            var settings = (SexyRevitViewModel)dialog.DataContext;
            settings.UseConsistentColors = loadedSettings.UseConsistentColors;
            settings.UseSmoothLines = loadedSettings.UseSmoothLines;
            settings.UseDetailFine = loadedSettings.UseDetailFine;
            settings.HideLevels = loadedSettings.HideLevels;
            settings.HideGrids = loadedSettings.HideGrids;
            settings.HideRefPoints = loadedSettings.HideRefPoints;
            settings.HideScopeBox = loadedSettings.HideScopeBox;
            settings.HideSectionBox = loadedSettings.HideSectionBox;
            settings.ConfigureSun = loadedSettings.ConfigureSun;

            // 2. Show Dialog
            if (dialog.ShowDialog() != true) return;

            // 3. Save Settings
            SettingsManager.Save(settings, "SexyRevitSettings.json");

            // 4. Run Service
            ShowLogWindow("Sexy Revit ✨");

            Log("Sexy Revit - View Beautification");
            Log("=================================");
            Log($"View: {view.Name}");
            Log("");

            var service = ServiceLocator.GetRequiredService<SexyRevitService>();
            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);
            service.ApplyBeauty(doc, view, settings.ToSettings(), reporter);

            UpdateProgress(100, "Complete!");
            Log("");
            Log("=== COMPLETE ===");
            Log("✨ Your view is now sexy!");
        }
    }
}
