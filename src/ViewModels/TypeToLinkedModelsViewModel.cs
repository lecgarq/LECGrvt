using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class TypeToLinkedModelsViewModel : BaseViewModel
    {
        private readonly ILinkedModelExportService _exportService;

        [ObservableProperty]
        private string _outputFolder = "";

        [ObservableProperty]
        private ObservableCollection<TypeGroupItem> _typeGroups = new();

        public bool CanRun => TypeGroups.Any(t => t.IsSelected) && !string.IsNullOrEmpty(OutputFolder);

        [RelayCommand]
        private void CheckAll()
        {
            foreach (var item in TypeGroups)
                item.IsSelected = true;
        }

        [RelayCommand]
        private void UncheckAll()
        {
            foreach (var item in TypeGroups)
                item.IsSelected = false;
        }

        public TypeToLinkedModelsViewModel(ILinkedModelExportService exportService)
        {
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            Title = "TYPE TO LINKED MODELS";
        }

        public void Initialize(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            Dictionary<string, List<ElementId>> groups = _exportService.GroupElementsByType(doc);

            TypeGroups.Clear();
            foreach (var kvp in groups.OrderBy(g => g.Key))
            {
                var item = new TypeGroupItem
                {
                    TypeName = kvp.Key,
                    ElementCount = kvp.Value.Count,
                    IsSelected = true
                };
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TypeGroupItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(CanRun));
                    }
                };
                TypeGroups.Add(item);
            }

            // Default output folder: same directory as the project file
            string? docPath = doc.PathName;
            if (!string.IsNullOrEmpty(docPath))
            {
                string? dir = System.IO.Path.GetDirectoryName(docPath);
                string projectName = System.IO.Path.GetFileNameWithoutExtension(docPath);
                if (dir != null)
                {
                    OutputFolder = System.IO.Path.Combine(dir, $"{projectName}_Links");
                }
            }
        }

        partial void OnOutputFolderChanged(string value)
        {
            OnPropertyChanged(nameof(CanRun));
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select output folder for linked model files",
                InitialDirectory = string.IsNullOrEmpty(OutputFolder) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : OutputFolder
            };

            if (dialog.ShowDialog() == true)
            {
                OutputFolder = dialog.FolderName;
            }
        }

        public Dictionary<string, List<ElementId>> GetSelectedGroups(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            Dictionary<string, List<ElementId>> allGroups = _exportService.GroupElementsByType(doc);
            var selected = new Dictionary<string, List<ElementId>>();

            foreach (TypeGroupItem item in TypeGroups.Where(t => t.IsSelected))
            {
                if (allGroups.TryGetValue(item.TypeName, out List<ElementId>? ids))
                {
                    selected[item.TypeName] = ids;
                }
            }

            return selected;
        }

        public override void Apply()
        {
            if (!CanRun) return;
            base.Apply();
        }
    }
}
