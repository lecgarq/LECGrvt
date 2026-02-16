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
        public SexyRevitService()
        {
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
                if (settings.ConfigureSun && view is View3D v3d)
                {
                    log("");
                    log("SUN SETTINGS");
                    progress(30, "Setting sun...");

                    try
                    {
                        SunAndShadowSettings? sunSettings = v3d.SunAndShadowSettings;
                        if (sunSettings != null)
                        {
                            sunSettings.SunAndShadowType = SunAndShadowType.StillImage;
                            log("  ✓ Sun Type: Still Image");
                        }
                    }
                    catch (Exception ex) { log($"  ⚠ Sun settings: {ex.Message}"); }
                }

                // 3. Hide Categories
                bool hideAnything = settings.HideLevels || settings.HideGrids || 
                                    settings.HideRefPoints || settings.HideScopeBox;
                
                if (hideAnything)
                {
                    log("");
                    log("HIDING ELEMENTS");
                    progress(50, "Hiding reference elements...");

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
                                log($"  ✓ Hidden: {cat.Name}");
                            }
                        }
                        catch { }
                    }
                }

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
