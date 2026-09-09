using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using LECG.Core;
using LECG.Core.Purge;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels;
using LECG.Views;
using LECG.Views.Base;
using LECG.Validation;
using System;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Commands
{
    /// <summary>
    /// Command to purge unused elements from the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PurgeCommand : RevitCommand
    {
        private const string PurgeSettingsFile = "PurgeDialogSettings.json";

        /// <summary>
        /// Auto-dismiss known-safe Revit TaskDialogs during purge operations via explicit whitelist.
        /// Unknown dialogs reach the user (reach-user default per CROSS-03).
        /// Whitelist entries are LOW-confidence until 06-DIALOG-DISCOVERY.md is updated
        /// with runtime-confirmed DialogId values.
        /// </summary>
        private static void OnDialogShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            DialogWhitelist.Global.Apply(e, ServiceLocator.GetRequiredService<ILogger>());
        }

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            UIApplication app = uiDoc.Application;

            PurgeDialogSettings? settings = GetOptionsFromCustomDialog() ?? GetOptionsFromFallbackDialog();
            if (settings == null) return;
            if (!TryValidateSettings(settings)) return;

            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);

            ShowLogWindow("Purge Unused");
            try
            {
                // Subscribe to DialogBoxShowing on the REAL UIApplication to suppress
                // "Extrusion is too thin" and similar modal TaskDialogs during LoadFamily.
                // Active for the entire purge operation — single subscription, not per-family.
                app.DialogBoxShowing += OnDialogShowing;

                var purgeService = ServiceLocator.GetRequiredService<PurgeService>();
                var options = new PurgeOptions(
                    settings.PurgeLineStyles,
                    settings.PurgeLinePatterns,
                    settings.PurgeFillPatterns,
                    settings.PurgeMaterials,
                    settings.PurgeLevels,
                    settings.PurgeParameters,
                    settings.PurgeGroups,
                    settings.PurgeGridTypes,
                    settings.PurgeLevelTypes,
                    settings.PurgeConstraints,
                    settings.PurgeUnplacedRooms,
                    settings.PurgeViewTemplates,
                    settings.PurgeViewFilters);

                if (settings.PassCount > 1)
                {
                    // Revit's native Purge Unused does not cover levels, grid/level types, groups,
                    // constraints, unplaced rooms, view templates, or view filters — so deep purge
                    // runs the native purge first, then the category-specific purges the user checked.
                    Log("Deep purge is using Revit native Purge Unused for every editable loaded family and for the project.");

                    var deepPurgeService = ServiceLocator.GetRequiredService<DeepPurgeService>();
                    deepPurgeService.Purge(doc, settings.PassCount, reporter);

                    Log("");
                    Log("--- CATEGORY PURGE ---");
                    purgeService.PurgeAll(doc, 1, options, reporter);
                }
                else
                {
                    purgeService.PurgeAll(doc, settings.PassCount, options, reporter);
                }
            }
            finally
            {
                // ALWAYS unsubscribe — even if purge throws
                app.DialogBoxShowing -= OnDialogShowing;
            }

            UpdateProgress(100, "Complete");
            Log("Purge Complete.");
        }

        private PurgeDialogSettings? GetOptionsFromCustomDialog()
        {
            try
            {
                var loaded = SettingsManager.Load<PurgeDialogSettings>(PurgeSettingsFile) ?? new PurgeDialogSettings();
                var view = ServiceLocator.GetRequiredService<PurgeView>();
                var vm = (PurgeViewModel)view.DataContext;
                vm.PurgeLineStyles = loaded.PurgeLineStyles;
                vm.PurgeLinePatterns = loaded.PurgeLinePatterns;
                vm.PurgeFillPatterns = loaded.PurgeFillPatterns;
                vm.PurgeMaterials = loaded.PurgeMaterials;
                vm.PurgeLevels = loaded.PurgeLevels;
                vm.PurgeParameters = loaded.PurgeParameters;
                vm.PurgeGroups = loaded.PurgeGroups;
                vm.PurgeGridTypes = loaded.PurgeGridTypes;
                vm.PurgeLevelTypes = loaded.PurgeLevelTypes;
                vm.PurgeConstraints = loaded.PurgeConstraints;
                vm.PurgeUnplacedRooms = loaded.PurgeUnplacedRooms;
                vm.PurgeViewTemplates = loaded.PurgeViewTemplates;
                vm.PurgeViewFilters = loaded.PurgeViewFilters;
                vm.IsDeepPurge = loaded.IsDeepPurge;

                bool? result = view.ShowDialog();
                if (result != true) return null;

                var settings = new PurgeDialogSettings
                {
                    PurgeLineStyles = vm.PurgeLineStyles,
                    PurgeLinePatterns = vm.PurgeLinePatterns,
                    PurgeFillPatterns = vm.PurgeFillPatterns,
                    PurgeMaterials = vm.PurgeMaterials,
                    PurgeLevels = vm.PurgeLevels,
                    PurgeParameters = vm.PurgeParameters,
                    PurgeGroups = vm.PurgeGroups,
                    PurgeGridTypes = vm.PurgeGridTypes,
                    PurgeLevelTypes = vm.PurgeLevelTypes,
                    PurgeConstraints = vm.PurgeConstraints,
                    PurgeUnplacedRooms = vm.PurgeUnplacedRooms,
                    PurgeViewTemplates = vm.PurgeViewTemplates,
                    PurgeViewFilters = vm.PurgeViewFilters,
                    IsDeepPurge = vm.IsDeepPurge
                };

                if (!TryValidateSettings(settings))
                {
                    return null;
                }

                SettingsManager.Save(settings, PurgeSettingsFile);
                return settings;
            }
            catch (Exception ex) when (IsExpectedPurgeDialogException(ex))
            {
                Log($"Purge WPF dialog failed. Using native fallback. {ex.Message}");
                return null;
            }
        }

        private static PurgeDialogSettings? GetOptionsFromFallbackDialog()
        {
            int selected = LecgDialog.ShowOptions(
                "LECG Purge Unused",
                "Choose purge mode. Safe mode avoids family-parameter purge.",
                "Safe Purge (Line Styles, Line Patterns, Fill Patterns, Materials)",
                "Deep Purge (+Levels, 3 passes)");

            if (selected < 0) return null;

            return new PurgeDialogSettings
            {
                PurgeLevels = selected == 1,
                IsDeepPurge = selected == 1
            };
        }

        private static bool TryValidateSettings(PurgeDialogSettings settings)
        {
            if (ValidationRules.TryValidate(settings, out string message))
            {
                return true;
            }

            LecgDialog.Show("Purge Validation", message);
            return false;
        }

        private static bool IsExpectedPurgeDialogException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
