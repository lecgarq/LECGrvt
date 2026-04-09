using System.Windows;
using LECG.Batch.Models;
using LECG.Batch.ViewModels;

namespace LECG.Batch.Views
{
    public partial class CloudModelBrowserView : LECG.Views.Base.LecgWindow
    {
        private readonly CloudModelBrowserViewModel _vm;

        public IReadOnlyList<ApsVersion> SelectedVersions => _vm.SelectedVersions.ToList();

        public CloudModelBrowserView(CloudModelBrowserViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
        }

        private void OnAddToQueueClick(object sender, RoutedEventArgs e)
        {
            foreach (ApsVersion version in VersionsList.SelectedItems.OfType<ApsVersion>())
                _vm.AddSelectedToQueue(version);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
