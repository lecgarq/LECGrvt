using Autodesk.Revit.DB;
using LECG.Utils;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class ChangeLevelViewModel : BaseViewModel
    {
        private readonly Document _doc;
        private readonly IChangeLevelService _service;
        
        // Internal storage for elements to be processed
        private List<Element> _selectedElements = new List<Element>();

        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private Level? _selectedLevel;

        [ObservableProperty]
        private string _targetText = "";

        public ObservableCollection<Level> Levels { get; } = new ObservableCollection<Level>();

        public bool CanRun => SelectedLevel != null && Selection.HasSelection;

        public ChangeLevelViewModel(Document doc, IChangeLevelService service)
        {
            _doc = doc;
            _service = service;
            Title = "CHANGE LEVEL";
            
            Selection.ElementName = "Toposolids";
            Selection.Filter = new SelectionFilters.ToposolidFilter(); // Depends on if this filter class is accessible
            
            LoadLevels();
        }

        private void LoadLevels()
        {
            var levels = _service.GetLevels(_doc);
            foreach (var level in levels)
            {
                Levels.Add(level);
            }
        }

        public void SetSelectedElements(List<Element> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            _selectedElements = elements;
            Selection.UpdateSelection(elements.Count);
        }

        [RelayCommand]
        private void Run()
        {
            if (SelectedLevel == null || !_selectedElements.Any()) return;

            try
            {
                _service.ChangeLevel(_doc, _selectedElements, SelectedLevel);
                CloseAction?.Invoke();
            }
            catch (System.Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"Failed to change level: {ex.Message}");
            }
        }
    }
}
