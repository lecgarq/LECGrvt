using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using LECG.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services.Interfaces;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

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

        [ObservableProperty]
        private bool _replaceInPlace = false;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public IList<Reference> SelectedRefs { get; private set; } = new List<Reference>();
        
        public bool ShouldRun { get; private set; }
        public bool CanRun => Selection.HasSelection && !string.IsNullOrWhiteSpace(TemplatePath);

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

        public void SetSelection(IList<Reference> refs, Document doc)
        {
            ArgumentNullException.ThrowIfNull(refs);
            ArgumentNullException.ThrowIfNull(doc);

            SelectedRefs = refs;
            Selection.UpdateSelection(refs.Count);
            
            if (refs.Any())
            {
                FamilyInstance? instance = doc.GetElement(refs.First()) as FamilyInstance;
                if (instance != null)
                {
                    if (string.IsNullOrWhiteSpace(NewFamilyName))
                        NewFamilyName = $"{instance.Symbol.Family.Name}_Converted";
                    
                    if (string.IsNullOrWhiteSpace(TemplatePath))
                        TemplatePath = _service.GetTargetTemplatePath(doc.Application, instance.Category);
                }
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

        protected override void Apply()
        {
            if (string.IsNullOrWhiteSpace(TemplatePath) || !File.Exists(TemplatePath))
            {
                return;
            }

            ShouldRun = true;
            CloseAction?.Invoke();
        }

        protected override void Cancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
