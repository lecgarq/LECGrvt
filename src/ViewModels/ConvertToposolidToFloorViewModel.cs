using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;

namespace LECG.ViewModels
{
    public partial class ConvertToposolidToFloorViewModel : BaseViewModel
    {
        private Document? _doc;
        private readonly IConversionService _service;
        private List<ElementId> _selectedElementIds = new List<ElementId>();

        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private ElementType? _selectedType;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private Level? _selectedLevel;

        [ObservableProperty]
        private bool _deleteSource = true;

        public ObservableCollection<ElementType> TargetTypes { get; } = new ObservableCollection<ElementType>();
        public ObservableCollection<Level> Levels { get; } = new ObservableCollection<Level>();
        public ObservableCollection<string> SelectedElementSummaries { get; } = new ObservableCollection<string>();

        public bool CanRun => _doc != null && SelectedType != null && SelectedLevel != null && Selection.HasSelection;

        public ConvertToposolidToFloorViewModel(IConversionService service)
        {
            _service = service;
            Title = "TOPOSOLID TO FLOOR";

            Selection.ElementName = "Toposolids";
            Selection.Filter = new SelectionFilters.ToposolidFilter();

            Selection.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Selection.HasSelection))
                    OnPropertyChanged(nameof(CanRun));
            };
        }

        public void Initialize(Document doc)
        {
            _doc = doc;
            LoadTargetTypes();
            LoadLevels();
        }

        private void LoadTargetTypes()
        {
            if (_doc == null) return;
            TargetTypes.Clear();
            var types = _service.GetFloorTypes(_doc);
            foreach (var type in types)
            {
                TargetTypes.Add(type);
            }

            if (TargetTypes.Count > 0)
                SelectedType = TargetTypes[0];
        }

        private void LoadLevels()
        {
            if (_doc == null) return;
            Levels.Clear();
            var levels = _service.GetLevels(_doc);
            foreach (var level in levels)
            {
                Levels.Add(level);
            }

            if (Levels.Count > 0)
                SelectedLevel = Levels[0];
        }

        public void SetSelectedElements(IEnumerable<Element> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            List<Element> selectedElements = elements
                .Where(element => element != null)
                .ToList();

            _selectedElementIds = selectedElements
                .Select(element => element.Id)
                .Distinct()
                .ToList();

            Selection.UpdateSelection(_selectedElementIds.Count);
            UpdateSelectedElementSummaries(selectedElements);

            if (selectedElements.Count > 0)
            {
                string? sourceTypeName = selectedElements
                    .Select(GetElementTypeName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Select(group => group.Key)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(sourceTypeName))
                {
                    var match = _service.FindMatchingType(sourceTypeName, TargetTypes.ToList<ElementType>());
                    if (match != null)
                        SelectedType = match;
                }

                // Auto-select the level of the first element
                var sourceLevel = GetElementLevel(selectedElements[0]);
                if (sourceLevel != null)
                {
                    var matchingLevel = Levels.FirstOrDefault(l => l.Id == sourceLevel.Id);
                    if (matchingLevel != null)
                        SelectedLevel = matchingLevel;
                }
            }
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            return _selectedElementIds
                .Select(doc.GetElement)
                .Where(element => element != null)
                .ToList()!;
        }

        private static string? GetElementTypeName(Element element)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return null;
            ElementType? type = element.Document.GetElement(typeId) as ElementType;
            return type?.Name;
        }

        private static Level? GetElementLevel(Element element)
        {
            Parameter? levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (levelParam == null)
                levelParam = element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);

            if (levelParam != null)
            {
                ElementId levelId = levelParam.AsElementId();
                if (levelId != ElementId.InvalidElementId)
                    return element.Document.GetElement(levelId) as Level;
            }

            return null;
        }

        private void UpdateSelectedElementSummaries(IEnumerable<Element> elements)
        {
            SelectedElementSummaries.Clear();

            foreach (Element element in elements)
            {
                SelectedElementSummaries.Add(DescribeElement(element));
            }
        }

        private static string DescribeElement(Element element)
        {
            string category = element.Category?.Name ?? element.GetType().Name;
            string typeName = GetElementTypeName(element) ?? "Unknown Type";
            string levelName = GetElementLevel(element)?.Name ?? "No Level";
            string name = string.IsNullOrWhiteSpace(element.Name) ? category : element.Name;

            return $"ID {element.Id} | {name} | Type: {typeName} | Level: {levelName}";
        }
    }
}
