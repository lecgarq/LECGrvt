using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels
{
    public partial class PurgeViewModel : BaseViewModel
    {
        [ObservableProperty]
        private bool _purgeLineStyles = true;

        [ObservableProperty]
        private bool _purgeFillPatterns = true;

        [ObservableProperty]
        private bool _purgeMaterials = true;

        [ObservableProperty]
        private bool _purgeLevels = false; // Default to false for safety

        public PurgeViewModel()
        {
            Title = "PURGE UNUSED";
        }
    }
}
