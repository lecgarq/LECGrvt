using LECG.ViewModels;

namespace LECG.Views
{
    public partial class SexyRevitView : Base.LecgWindow
    {
        public SexyRevitView(SexyRevitViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            InitializeComponent();
            DataContext = vm;
            BindDialogClose(vm, () => vm.ShouldRun);
        }
    }
}
