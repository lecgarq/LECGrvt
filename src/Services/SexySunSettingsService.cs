using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class SexySunSettingsService : ISexySunSettingsService
    {
        public void Apply(View view, SexyRevitSettings settings, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(reporter);

            if (!(settings.ConfigureSun && view is View3D v3d))
            {
                return;
            }

            reporter.Log("");
            reporter.Log("SUN SETTINGS");
            reporter.Report("Setting sun...", 30);

            try
            {
                SunAndShadowSettings? sunSettings = v3d.SunAndShadowSettings;
                if (sunSettings != null)
                {
                    sunSettings.SunAndShadowType = SunAndShadowType.StillImage;
                    reporter.Log("Sun Type: Still Image");
                }
            }
            catch (Exception ex) when (IsExpectedSunSettingsException(ex))
            {
                reporter.LogWarning($"Sun settings: {ex.Message}");
            }
        }

        private static bool IsExpectedSunSettingsException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
