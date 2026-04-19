using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Models;

namespace LECG.ViewModels
{
    public partial class SexyRevitViewModel : BaseViewModel
    {
        [RelayCommand]
        private void CheckAll()
        {
            UseConsistentColors = true;
            UseSmoothLines = true;
            UseDetailFine = true;
            HideLevels = true;
            HideGrids = true;
            HideRefPoints = true;
            HideScopeBox = true;
            HideSectionBox = true;
            ConfigureSun = true;
        }

        [RelayCommand]
        private void UncheckAll()
        {
            UseConsistentColors = false;
            UseSmoothLines = false;
            UseDetailFine = false;
            HideLevels = false;
            HideGrids = false;
            HideRefPoints = false;
            HideScopeBox = false;
            HideSectionBox = false;
            ConfigureSun = false;
        }

        // Graphics
        [ObservableProperty] private bool _useConsistentColors = true;
        [ObservableProperty] private bool _useSmoothLines = true;
        [ObservableProperty] private bool _useDetailFine = true;

        // Hide Elements
        [ObservableProperty] private bool _hideLevels = true;
        [ObservableProperty] private bool _hideGrids = true;
        [ObservableProperty] private bool _hideRefPoints = true;
        [ObservableProperty] private bool _hideScopeBox = true;
        [ObservableProperty] private bool _hideSectionBox = true;

        // Lighting
        [ObservableProperty] private bool _configureSun = true;

        public SexyRevitViewModel()
        {
            Title = "SEXY REVIT";
        }

        public SexyRevitSettings ToSettings()
        {
            return new SexyRevitSettings(
                UseConsistentColors,
                UseSmoothLines,
                UseDetailFine,
                HideLevels,
                HideGrids,
                HideRefPoints,
                HideScopeBox,
                HideSectionBox,
                ConfigureSun);
        }
    }
}
