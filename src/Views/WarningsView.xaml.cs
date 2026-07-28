using System;
using Autodesk.Revit.UI;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class WarningsView : LecgWindow
    {
        public WarningsView(WarningsViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            InitializeComponent();
            DataContext = vm;
            BindClose(vm);
        }

        public override void Initialize(UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            base.Initialize(uiDoc);
            (DataContext as WarningsViewModel)?.Initialize(uiDoc);
        }
    }
}
