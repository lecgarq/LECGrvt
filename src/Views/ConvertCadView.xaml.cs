using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views.Base;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System;

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
                if (IsLoaded)
                {
                    try { DialogResult = vm.ShouldRun; } catch { Close(); }
                }
                else
                {
                    Close();
                }
            };

            // Hook up selection request
            vm.Selection.OnRequestSelect += (s, e) => 
            {
                Hide();
                try 
                {
                    Reference r = _uiDoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        "Select an Imported CAD or Link");
                    
                    if (r != null)
                    {
                        Element el = _uiDoc.Document.GetElement(r);
                        vm.SetSelection(el);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally
                {
                    Show();
                    Activate();
                }
            };
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
