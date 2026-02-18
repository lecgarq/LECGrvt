using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using LECG.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services.Interfaces;
using Microsoft.Win32;
using System.IO;

namespace LECG.ViewModels
{
    public partial class ConvertFamilyViewModel : BaseViewModel
    {
        private readonly IFamilyConversionService _service;
        
        [ObservableProperty]
        private string _newFamilyName = "";

        [ObservableProperty]
        private string _templatePath = "";

        [ObservableProperty]
        private bool _isTemporary = true;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public Reference? SelectedRef { get; private set; }
        
        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection && !string.IsNullOrWhiteSpace(NewFamilyName) && !string.IsNullOrWhiteSpace(TemplatePath);

        public ConvertFamilyViewModel(IFamilyConversionService service)
        {
            _service = service;
            Title = "CONVERT FAMILY";
            Selection.ElementName = "Family Instance";
            
            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
            this.PropertyChanged += (s, e) => { 
                if (e.PropertyName == nameof(NewFamilyName) || e.PropertyName == nameof(TemplatePath)) 
                    OnPropertyChanged(nameof(CanRun)); 
            };
        }

        public void SetSelection(Reference r, Document doc)
        {
            ArgumentNullException.ThrowIfNull(r);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedRef = r;
            Selection.UpdateSelection(1);
            
            FamilyInstance? instance = doc.GetElement(r) as FamilyInstance;
            if (instance != null)
            {
                NewFamilyName = $"{instance.Symbol.Family.Name}_Converted";
                TemplatePath = _service.GetTargetTemplatePath(doc.Application, instance.Category);
            }
        }

        [RelayCommand]
        private void BrowseTemplate()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "Select Family Template"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TemplatePath = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private void ExecuteRun()
        {
            if (string.IsNullOrWhiteSpace(TemplatePath) || !File.Exists(TemplatePath))
            {
                return;
            }

            ShouldRun = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void ExecuteCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
