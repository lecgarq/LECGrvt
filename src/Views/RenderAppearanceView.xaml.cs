using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class RenderAppearanceView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public RenderAppearanceView(RenderAppearanceViewModel vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
            // Listen to VM events to handle window behavior
            vm.CloseAction = () => 
            {
                if (IsLoaded)
                {
                    try { DialogResult = vm.ShouldRun; } catch { Close(); }
                }
                else
                {
                    Close();
                }
            };
        }
    }
}
