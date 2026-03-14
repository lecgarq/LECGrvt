using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexyCategoryVisibilityService : ISexyCategoryVisibilityService
    {
        public void Apply(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter)
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
    }
}
