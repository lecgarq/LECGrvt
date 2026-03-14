using Autodesk.Revit.DB;
using LECG.Utils;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.ViewModels.Components;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class ChangeLevelViewModel : BaseViewModel
    {
        private Document? _doc;
        private readonly IChangeLevelService _service;
        
        // Internal storage for elements to be processed
        private List<Element> _selectedElements = new List<Element>();

        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private Level? _selectedLevel;

        [ObservableProperty]
        private string _targetText = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
        private string _validationMessage = string.Empty;

        public ObservableCollection<Level> Levels { get; } = new ObservableCollection<Level>();

        public bool CanRun => SelectedLevel != null && Selection.HasSelection;
        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        public ChangeLevelViewModel(IChangeLevelService service)
        {
            _service = service;
            Title = "CHANGE LEVEL";
            
            Selection.ElementName = "Toposolids";
            Selection.Filter = new SelectionFilters.ToposolidFilter(); // Depends on if this filter class is accessible
        }

        public void Initialize(Document doc)
        {
            _doc = doc;
            LoadLevels();
        }

        private void LoadLevels()
        {
            if (_doc == null) return;
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
            ValidationMessage = string.Empty;
        }

        public override void Apply()
        {
            if (_doc == null || SelectedLevel == null || !_selectedElements.Any()) return;

            try
            {
                ValidationMessage = string.Empty;
                _service.ChangeLevel(_doc, _selectedElements, SelectedLevel);
                base.Apply();
            }
            catch (System.Exception ex)
            {
                ValidationMessage = $"Failed to change level: {ex.Message}";
            }
        }
    }
}
