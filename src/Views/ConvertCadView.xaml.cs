using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace LECG.Views
{
    public partial class ConvertCadView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public ConvertCadView(ConvertCadViewModel vm, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(vm);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = vm;
            _uiDoc = uiDoc;
            
            // Allow the ViewModel to close this window
            vm.CloseAction = () => 
            {
                this.Close();
            };
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
