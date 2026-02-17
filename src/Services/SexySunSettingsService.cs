using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class SexySunSettingsService : ISexySunSettingsService
    {
        public void Apply(View view, SexyRevitViewModel settings, Action<string> log, Action<double, string> progress)
        {
            if (!(settings.ConfigureSun && view is View3D v3d))
            {
                return;
            }

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
            catch (Exception ex)
            {
                log($"  ⚠ Sun settings: {ex.Message}");
            }
        }
    }
}
