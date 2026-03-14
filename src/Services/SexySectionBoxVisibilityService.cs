using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexySectionBoxVisibilityService : ISexySectionBoxVisibilityService
    {
        public void Apply(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter)
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
