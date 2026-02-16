using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels
{
    public partial class SexyRevitViewModel : BaseViewModel
    {
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
    }
}
