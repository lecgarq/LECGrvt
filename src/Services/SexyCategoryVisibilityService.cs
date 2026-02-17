using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class SexyCategoryVisibilityService : ISexyCategoryVisibilityService
    {
        public void Apply(Document doc, View view, SexyRevitViewModel settings, Action<string> log, Action<double, string> progress)
        {
            bool hideAnything = settings.HideLevels || settings.HideGrids ||
                                settings.HideRefPoints || settings.HideScopeBox;

            if (!hideAnything)
            {
                return;
            }

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
                catch
                {
                }
            }
        }
    }
}
