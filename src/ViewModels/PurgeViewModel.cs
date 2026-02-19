using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
        private bool _purgeLevels = false; 

        [ObservableProperty]
        private bool _isDeepPurge = true;

        public PurgeViewModel()
        {
            Title = "PURGE UNUSED";
        }

        [RelayCommand]
        private void Apply()
        {
            CloseAction?.Invoke();
        }
    }
}
