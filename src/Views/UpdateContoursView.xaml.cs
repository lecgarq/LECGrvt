using System.Windows;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class UpdateContoursView : LecgWindow
    {
        public UpdateContoursView(UpdateContoursViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseAction = () =>
            {
                DialogResult = vm.ShouldRun;
                Close();
            };
        }
    }
}
