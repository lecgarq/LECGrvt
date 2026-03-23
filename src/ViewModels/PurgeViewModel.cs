using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    public partial class PurgeViewModel : BaseViewModel
    {
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
