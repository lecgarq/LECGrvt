using System;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class TypeToLinkedModelsView : LecgWindow
    {
        public TypeToLinkedModelsView(TypeToLinkedModelsViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            InitializeComponent();
            DataContext = vm;
            BindDialogClose(vm, () => vm.ShouldRun);
        }
    }
}
