using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Models;

namespace LECG.ViewModels
{
    public partial class RenderAppearanceViewModel : BaseViewModel
    {
        public RenderAppearanceViewModel()
        {
            Title = "SYNC RENDER APPEARANCE";
        }

        public bool CanRun => true;

        public RenderAppearanceSettings ToSettings()
        {
            return new RenderAppearanceSettings(
                SkipCompliant: true,
                UseRenderAppearanceForShading: true,
                MatchShadingColorToRenderAppearance: true,
                SetSurfacePatternsToSolidFill: true,
                SetCutPatternsToSolidFill: true);
        }
    }
}
