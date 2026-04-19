using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class PurgeViewModel : BaseViewModel
    {
        [RelayCommand]
        private void CheckAll()
        {
            PurgeLineStyles = true;
            PurgeLinePatterns = true;
            PurgeFillPatterns = true;
            PurgeMaterials = true;
            PurgeLevels = true;
            PurgeGroups = true;
            PurgeGridTypes = true;
            PurgeLevelTypes = true;
            PurgeConstraints = true;
            PurgeUnplacedRooms = true;
            PurgeViewTemplates = true;
            PurgeViewFilters = true;
            PurgeParameters = true;
        }

        [RelayCommand]
        private void UncheckAll()
        {
            PurgeLineStyles = false;
            PurgeLinePatterns = false;
            PurgeFillPatterns = false;
            PurgeMaterials = false;
            PurgeLevels = false;
            PurgeGroups = false;
            PurgeGridTypes = false;
            PurgeLevelTypes = false;
            PurgeConstraints = false;
            PurgeUnplacedRooms = false;
            PurgeViewTemplates = false;
            PurgeViewFilters = false;
            PurgeParameters = false;
        }

        [ObservableProperty]
        private bool _purgeLineStyles = true;

        [ObservableProperty]
        private bool _purgeLinePatterns = true;

        [ObservableProperty]
        private bool _purgeFillPatterns = true;

        [ObservableProperty]
        private bool _purgeMaterials = true;

        [ObservableProperty]
        private bool _purgeLevels = false;

        [ObservableProperty]
        private bool _isDeepPurge = true;

        [ObservableProperty]
        private bool _purgeParameters = false;

        [ObservableProperty]
        private bool _purgeGroups = false;

        [ObservableProperty]
        private bool _purgeGridTypes = false;

        [ObservableProperty]
        private bool _purgeLevelTypes = false;

        [ObservableProperty]
        private bool _purgeConstraints = false;

        [ObservableProperty]
        private bool _purgeUnplacedRooms = false;

        [ObservableProperty]
        private bool _purgeViewTemplates = false;

        [ObservableProperty]
        private bool _purgeViewFilters = false;

        public PurgeViewModel()
        {
            Title = "PURGE UNUSED";
        }

    }
}
