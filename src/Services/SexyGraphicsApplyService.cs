using System;
using Autodesk.Revit.DB;
using LECG.Core.Graphics;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class SexyGraphicsApplyService : ISexyGraphicsApplyService
    {
        public void Apply(
            IViewGraphicsFacade view,
            SexyRevitViewModel settings,
            Action<string> log,
            Action<double, string> progress)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(log);
            ArgumentNullException.ThrowIfNull(progress);

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
