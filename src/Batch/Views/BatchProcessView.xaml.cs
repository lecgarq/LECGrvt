using System.Windows;
using LECG.Batch.ViewModels;
using LECG.Core;

namespace LECG.Batch.Views
{
    public partial class BatchProcessView : LECG.Views.Base.LecgWindow
    {
        private readonly BatchProcessViewModel _vm;

        public BatchProcessView(BatchProcessViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
        }

        private void OnBrowseModelsClick(object sender, RoutedEventArgs e)
        {
            var browser = ServiceLocator.GetRequiredService<CloudModelBrowserView>();
            browser.Owner = this;
            bool? result = browser.ShowDialog();

            if (result == true || browser.SelectedVersions.Count > 0)
            {
                _vm.AddJobsToQueue(browser.SelectedVersions);
            }
        }
    }
}
