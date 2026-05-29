using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexyRevitService : ISexyRevitService
    {
        private readonly ISexySunSettingsService _sunSettingsService;
        private readonly ISexyCategoryVisibilityService _categoryVisibilityService;
        private readonly ISexySectionBoxVisibilityService _sectionBoxVisibilityService;
        private readonly ISexyGraphicsApplyService _sexyGraphicsApplyService;
        private readonly ITransactionService _transactionService;

        public SexyRevitService(ISexySunSettingsService sunSettingsService, ISexyCategoryVisibilityService categoryVisibilityService, ISexySectionBoxVisibilityService sectionBoxVisibilityService, ISexyGraphicsApplyService sexyGraphicsApplyService, ITransactionService transactionService)
        {
            _sunSettingsService = sunSettingsService;
            _categoryVisibilityService = categoryVisibilityService;
            _sectionBoxVisibilityService = sectionBoxVisibilityService;
            _sexyGraphicsApplyService = sexyGraphicsApplyService;
            _transactionService = transactionService;
        }

        public void ApplyBeauty(Document doc, View view, SexyRevitSettings settings, IProgressReporter reporter)
        {
            if (view == null) return;

            _transactionService.Run(doc, "Sexy Revit", currentDoc =>
            {
                // 1. Graphics Settings (Textures, Shadows, Lighting)
                _sexyGraphicsApplyService.Apply(new RevitViewGraphicsFacade(view), settings, reporter);

                // 2. Sun Settings (3D only)
                _sunSettingsService.Apply(view, settings, reporter);

                // 3. Hide Categories
                _categoryVisibilityService.Apply(currentDoc, view, settings, reporter);

                // 4. Section Box (3D Only)
                _sectionBoxVisibilityService.Apply(currentDoc, view, settings, reporter);
            });
        }

    }
}




