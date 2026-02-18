using System.Windows;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class FilterCopyView : LecgWindow
    {
        public FilterCopyView(FilterCopyViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = () => Close();
        }
    }
}
