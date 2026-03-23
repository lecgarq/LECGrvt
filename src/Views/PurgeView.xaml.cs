using LECG.ViewModels;

namespace LECG.Views
{
    public partial class PurgeView : Base.LecgWindow
    {
        public PurgeView(PurgeViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            InitializeComponent();
            DataContext = vm;
            BindDialogClose(vm, () => vm.ShouldRun);
        }
    }
}
