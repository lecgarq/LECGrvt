using System;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class SubstanceBatchView : LecgWindow
    {
        public SubstanceBatchView(SubstanceBatchViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            InitializeComponent();
            DataContext = viewModel;
            BindDialogClose(viewModel, () => viewModel.ShouldRun || viewModel.ShouldRepath);
        }
    }
}
