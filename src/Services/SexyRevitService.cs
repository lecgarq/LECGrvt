using System;
using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexyRevitService : ISexyRevitService
    {
        private readonly ISexySunSettingsService _sunSettingsService;
        private readonly ISexyCategoryVisibilityService _categoryVisibilityService;
        private readonly ISexySectionBoxVisibilityService _sectionBoxVisibilityService;
        private readonly ISexyGraphicsApplyService _sexyGraphicsApplyService;

        public SexyRevitService() : this(new SexySunSettingsService(), new SexyCategoryVisibilityService(), new SexySectionBoxVisibilityService(), new SexyGraphicsApplyService())
        {
        }

        public SexyRevitService(ISexySunSettingsService sunSettingsService, ISexyCategoryVisibilityService categoryVisibilityService, ISexySectionBoxVisibilityService sectionBoxVisibilityService, ISexyGraphicsApplyService sexyGraphicsApplyService)
        {
            _sunSettingsService = sunSettingsService;
            _categoryVisibilityService = categoryVisibilityService;
            _sectionBoxVisibilityService = sectionBoxVisibilityService;
            _sexyGraphicsApplyService = sexyGraphicsApplyService;
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
                _sexyGraphicsApplyService.Apply(new RevitViewGraphicsFacade(view), settings, log, progress);

                // 2. Sun Settings (3D only)
                _sunSettingsService.Apply(view, settings, log, progress);

                // 3. Hide Categories
                _categoryVisibilityService.Apply(doc, view, settings, log, progress);

                // 4. Section Box (3D Only)
                _sectionBoxVisibilityService.Apply(doc, view, settings, log, progress);

                t.Commit();
            }
        }

    }
}




