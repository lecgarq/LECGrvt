using System;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class PbrMaterialCreatorView : LecgWindow
    {
        public PbrMaterialCreatorView(PbrMaterialCreatorViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            DataContext = viewModel;
            BindDialogClose(viewModel, () => viewModel.ShouldRun);
        }
    }
}
