using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core.Graphics;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class SexyRevitService
    {
        private readonly ITransactionService _transactionService;

        public SexyRevitService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public void ApplyBeauty(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter)
        {
            if (view == null) return;

            _transactionService.Run(doc, "Sexy Revit", currentDoc =>
            {
                // 1. Graphics Settings (Textures, Shadows, Lighting)
                ApplyGraphics(new RevitViewGraphicsFacade(view), settings, reporter);

                // 2. Sun Settings (3D only)
                ApplySunSettings(view, settings, reporter);

                // 3. Hide Categories
                ApplyCategoryVisibility(currentDoc, view, settings, reporter);

                // 4. Section Box (3D Only)
                ApplySectionBoxVisibility(currentDoc, view, settings, reporter);
            });
        }

        private static void ApplyGraphics(
            IViewGraphicsFacade view,
            SexyRevitSettings settings,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            var decision = SexyRevitGraphicsPolicy.Evaluate(
                new SexyRevitGraphicsSettings(settings.UseConsistentColors, settings.UseDetailFine));

            if (!decision.ShouldApply) return;

            reporter.Report("Applying sexy graphics...", 10);

            try
            {
                if (decision.DisplayStyle == CoreDisplayStyle.Realistic)
                {
                    view.DisplayStyle = ViewDisplayStyle.Realistic;
                }
            }
            catch
            {
                reporter.LogWarning("  Could not set display style");
            }

            foreach (var message in decision.Messages)
            {
                reporter.Log(message);
            }

            if (decision.DetailLevel == CoreDetailLevel.Fine)
            {
                view.DetailLevel = ViewDetailLevelFacade.Fine;
            }
        }

        private static void ApplySunSettings(View view, SexyRevitSettings settings, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            if (!(settings.ConfigureSun && view is View3D v3d))
            {
                return;
            }

            reporter.Log("");
            reporter.Log("SUN SETTINGS");
            reporter.Report("Setting sun...", 30);

            try
            {
                SunAndShadowSettings? sunSettings = v3d.SunAndShadowSettings;
                if (sunSettings != null)
                {
                    sunSettings.SunAndShadowType = SunAndShadowType.StillImage;
                    reporter.Log("Sun Type: Still Image");
                }
            }
            catch (Exception ex) when (IsExpectedSunSettingsException(ex))
            {
                reporter.LogWarning($"Sun settings: {ex.Message}");
            }
        }

        private static bool IsExpectedSunSettingsException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static void ApplyCategoryVisibility(
            Document doc,
            View view,
            SexyRevitSettings settings,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            bool hideAnything = settings.HideLevels || settings.HideGrids ||
                                settings.HideRefPoints || settings.HideScopeBox;

            if (!hideAnything)
            {
                return;
            }

            reporter.Log("");
            reporter.Log("HIDING ELEMENTS");
            reporter.Report("Hiding reference elements...", 50);

            List<BuiltInCategory> categoriesToHide = new List<BuiltInCategory>();

            if (settings.HideLevels) categoriesToHide.Add(BuiltInCategory.OST_Levels);
            if (settings.HideGrids) categoriesToHide.Add(BuiltInCategory.OST_Grids);
            if (settings.HideRefPoints)
            {
                categoriesToHide.Add(BuiltInCategory.OST_ProjectBasePoint);
                categoriesToHide.Add(BuiltInCategory.OST_SharedBasePoint);
            }

            if (settings.HideScopeBox) categoriesToHide.Add(BuiltInCategory.OST_VolumeOfInterest);

            foreach (BuiltInCategory bic in categoriesToHide)
            {
                try
                {
                    Category? cat = Category.GetCategory(doc, bic);
                    if (cat != null && view.CanCategoryBeHidden(cat.Id))
                    {
                        view.SetCategoryHidden(cat.Id, true);
                        reporter.Log($"Hidden: {cat.Name}");
                    }
                }
                catch
                {
                }
            }
        }

        private static void ApplySectionBoxVisibility(
            Document doc,
            View view,
            SexyRevitSettings settings,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            if (!(settings.HideSectionBox && view is View3D))
            {
                return;
            }

            reporter.Log("");
            reporter.Log("VIEW OPTIONS");
            reporter.Report("Configuring view...", 70);

            try
            {
                Category? sectionBoxCat = Category.GetCategory(doc, BuiltInCategory.OST_SectionBox);
                if (sectionBoxCat != null && view.CanCategoryBeHidden(sectionBoxCat.Id))
                {
                    view.SetCategoryHidden(sectionBoxCat.Id, true);
                    reporter.Log("Section Box: Hidden");
                }
            }
            catch
            {
            }
        }

    }
}




