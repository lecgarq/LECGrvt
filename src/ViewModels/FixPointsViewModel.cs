using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core;
using LECG.ViewModels.Components;

namespace LECG.ViewModels
{
    public partial class FixPointsViewModel : BaseViewModel
    {
        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<string> SelectedElementSummaries { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private int _sensitivity = 3;

        public bool CanRun => Selection.HasSelection;

        public string SensitivityLabel => Sensitivity switch
        {
            1 => "Conservative",
            2 => "Moderate",
            3 => "Balanced",
            4 => "Sensitive",
            5 => "Aggressive",
            _ => "Balanced"
        };

        partial void OnSensitivityChanged(int value)
        {
            OnPropertyChanged(nameof(SensitivityLabel));
        }

        public FixPointsViewModel()
        {
            Title = "FIX POINTS";
            Selection.ElementName = "Floors/Toposolids";
            Selection.Filter = new SelectionFilters.SlabFilter();

            Selection.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectionViewModel.HasSelection))
                {
                    OnPropertyChanged(nameof(CanRun));
                }
            };
        }

        public void SetSelectedElements(IEnumerable<Element> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            List<Element> selectedElements = elements
                .Where(element => element != null)
                .ToList();

            SelectedElementIds = selectedElements
                .Select(element => element.Id)
                .Distinct()
                .ToList();

            Selection.UpdateSelection(SelectedElementIds.Count);
            UpdateSelectedElementSummaries(selectedElements);
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            return SelectedElementIds
                .Select(doc.GetElement)
                .Where(element => element != null)
                .ToList()!;
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
            string levelName = GetElementLevel(element)?.Name ?? "No Level";
            string name = string.IsNullOrWhiteSpace(element.Name) ? category : element.Name;

            return $"ID {element.Id} | {name} | Level: {levelName}";
        }

        private static Level? GetElementLevel(Element element)
        {
            Parameter? levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);

            if (levelParam == null)
            {
                return null;
            }

            ElementId levelId = levelParam.AsElementId();
            if (levelId == ElementId.InvalidElementId)
            {
                return null;
            }

            return element.Document.GetElement(levelId) as Level;
        }
    }
}
