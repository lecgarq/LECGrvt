using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class SexySectionBoxVisibilityService : ISexySectionBoxVisibilityService
    {
        public void Apply(Document doc, View view, SexyRevitViewModel settings, Action<string> log, Action<double, string> progress)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(log);
            ArgumentNullException.ThrowIfNull(progress);

            if (!(settings.HideSectionBox && view is View3D))
            {
                return;
            }

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
            catch
            {
            }
        }
    }
}
