using Autodesk.Revit.DB;
using LECG.Core.Graphics;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SexyGraphicsApplyService : ISexyGraphicsApplyService
    {
        public void Apply(
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
    }
}
