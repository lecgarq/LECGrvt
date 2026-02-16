using System.Windows;
using System.Windows.Input;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class SearchReplaceView : LecgWindow
    {
        public SearchReplaceView(SearchReplaceViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
