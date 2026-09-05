using System.Windows;
using LECG.Batch.Models;
using LECG.Batch.ViewModels;

namespace LECG.Batch.Views
{
    public partial class CloudModelBrowserView : LECG.Views.Base.LecgWindow
    {
        private readonly CloudModelBrowserViewModel _vm;
        private bool _isInitialized;

        public IReadOnlyList<ApsVersion> SelectedVersions => _vm.SelectedVersions.ToList();

        public CloudModelBrowserView(CloudModelBrowserViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            await _vm.EnsureInitializedAsync();
        }

        private void OnAddToQueueClick(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCount == 0)
            {
                _vm.StatusMessage = "Check at least one model to add to the queue.";
                return;
            }

            _vm.ConfirmSelection();
            DialogResult = true;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
