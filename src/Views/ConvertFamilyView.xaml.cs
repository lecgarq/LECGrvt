using LECG.ViewModels;
using LECG.Views.Base;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using LECG.Utils;
using System;
using System.Collections.Generic;

namespace LECG.Views
{
    public partial class ConvertFamilyView : LecgWindow
    {
        private readonly UIDocument _uiDoc;

        public ConvertFamilyView(ConvertFamilyViewModel viewModel, UIDocument uiDoc)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(uiDoc);

            InitializeComponent();
            DataContext = viewModel;
            _uiDoc = uiDoc;
            
             // Listen to VM events to handle window behavior
            viewModel.CloseAction = () => 
            {
                if (IsLoaded)
                {
                    try { DialogResult = viewModel.ShouldRun; } catch { Close(); }
                }
                else
                {
                    Close();
                }
            };

            // Selection request
            viewModel.Selection.OnRequestSelect += (s, e) => {
                Hide();
                try 
                {
                    Autodesk.Revit.UI.Selection.ISelectionFilter filter = new LECG.Utils.FamilyInstanceFilter();
                    
                    var refs = _uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        filter, 
                        "Select hosted family instances to convert.");
                    
                    viewModel.SetSelection(refs, _uiDoc.Document);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                finally
                {
                    try
                    {
                        this.Visibility = System.Windows.Visibility.Visible;
                        Activate();
                    }
                    catch { }
                }
            };
        }
    }
}
