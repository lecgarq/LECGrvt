using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core.Graphics;
using LECG.ViewModels;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexyRevitService : ISexyRevitService
    {
        private readonly ISexySunSettingsService _sunSettingsService;
        private readonly ISexyCategoryVisibilityService _categoryVisibilityService;

        public SexyRevitService() : this(new SexySunSettingsService(), new SexyCategoryVisibilityService())
        {
        }

        public SexyRevitService(ISexySunSettingsService sunSettingsService, ISexyCategoryVisibilityService categoryVisibilityService)
        {
            _sunSettingsService = sunSettingsService;
            _categoryVisibilityService = categoryVisibilityService;
        }

        public void ApplyBeauty(Document doc, View view, SexyRevitViewModel settings, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            if (view == null) return;

            // Helper for logging/progress to handle nulls gracefully
            Action<string> log = logCallback ?? (_ => { });
            Action<double, string> progress = progressCallback ?? ((_, __) => { });

            using (Transaction t = new Transaction(doc, "Sexy Revit"))
            {
                t.Start();
                // 1. Graphics Settings (Textures, Shadows, Lighting)
                ApplyGraphicsAndLighting(new RevitViewGraphicsFacade(view), settings, log, progress);

                // 2. Sun Settings (3D only)
                _sunSettingsService.Apply(view, settings, log, progress);
                // 3. Hide Categories
                _categoryVisibilityService.Apply(doc, view, settings, log, progress);

                // 4. Section Box (3D Only)
                if (settings.HideSectionBox && view is View3D)
                {
                    log("");
                    log("VIEW OPTIONS");
                    progress(70, "Configuring view...");
                    
                    try
                    {
                        Category? sectionBoxCat = Category.GetCategory(doc, BuiltInCategory.OST_SectionBox);
                        if (sectionBoxCat != null && view.CanCategoryBeHidden(sectionBoxCat.Id))
                        {
                            view.SetCategoryHidden(sectionBoxCat.Id, true);
                            log("  ✓ Section Box: Hidden");
                        }
                    }
                    catch { }
                }

                t.Commit();
            }
        }

        internal static void ApplyGraphicsAndLighting(
            IViewGraphicsFacade view,
            SexyRevitViewModel settings,
            Action<string> log,
            Action<double, string> progress)
        {
            var decision = SexyRevitGraphicsPolicy.Evaluate(
                new SexyRevitGraphicsSettings(settings.UseConsistentColors, settings.UseDetailFine));

            if (!decision.ShouldApply) return;

            progress(10, "Applying sexy graphics...");

            try
            {
                if (decision.DisplayStyle == CoreDisplayStyle.Realistic)
                {
                    view.DisplayStyle = ViewDisplayStyle.Realistic;
                }
            }
            catch
            {
                log("  Could not set display style");
            }

            foreach (var message in decision.Messages)
            {
                log(message);
            }

            if (decision.DetailLevel == CoreDetailLevel.Fine)
            {
                view.DetailLevel = ViewDetailLevelFacade.Fine;
            }
        }
    }
}



