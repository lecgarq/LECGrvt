using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core;
using LECG.Services;
using LECG.ViewModels.Components;

namespace LECG.ViewModels
{
    public partial class FixPointsViewModel : BaseViewModel
    {
        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<ElementRowViewModel> RowItems { get; } = new ObservableCollection<ElementRowViewModel>();

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
            UpdateRowItems(selectedElements);
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            return SelectedElementIds
                .Select(doc.GetElement)
                .Where(element => element != null)
                .ToList()!;
        }

        private void UpdateRowItems(IEnumerable<Element> elements)
        {
            RowItems.Clear();

            foreach (Element element in elements)
            {
                var (name, category) = ElementLabelService.GetLabels(element);
                string status = ResolveStatus(element);
                RowItems.Add(new ElementRowViewModel
                {
                    Id = element.Id.Value,
                    Name = name,
                    Category = category,
                    Type = element.GetType().Name,
                    Status = status,
                    IsChecked = true
                });
            }
        }

        private static string ResolveStatus(Element element)
        {
            string levelName = GetElementLevel(element)?.Name ?? "No Level";
            return $"Level: {levelName}";
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
