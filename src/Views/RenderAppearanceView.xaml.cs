using Autodesk.Revit.UI;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class RenderAppearanceView : LecgWindow
    {
        public RenderAppearanceView(RenderAppearanceViewModel vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;

            BindDialogClose(vm, () => vm.ShouldRun);
        }
    }
}
