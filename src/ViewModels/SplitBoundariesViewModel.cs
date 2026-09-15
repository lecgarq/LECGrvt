using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.Input;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.ViewModels
{
    public partial class SplitBoundariesViewModel : BaseViewModel
    {
        private readonly SplitBoundariesService _splitBoundariesService;
        private readonly HashSet<long> _splittableIds = new HashSet<long>();
        private readonly Dictionary<long, SplitBoundaryInspection> _inspectionById = new();

        private string _inspectionStatus = "Inspect the model to find every eligible Floor and Toposolid.";
        public string InspectionStatus
        {
            get => _inspectionStatus;
            private set => SetProperty(ref _inspectionStatus, value);
        }

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<ElementRowViewModel> RowItems => Selection.RowItems;

        public bool CanRun => RowItems.Any(row => row.IsChecked && _splittableIds.Contains(row.Id));

        private RelayCommand? _applyCommandLocal;
        public override ICommand ApplyCommand => _applyCommandLocal ??= new RelayCommand(OnApply, () => CanRun);

        private ICommand? _cancelCommandLocal;
        public override ICommand CancelCommand => _cancelCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnCancel);

        public SplitBoundariesViewModel(SplitBoundariesService splitBoundariesService)
        {
            _splitBoundariesService = splitBoundariesService;
            Title = "SPLIT BOUNDARIES";
            Selection.ElementName = "Floors/Toposolids";
            Selection.Filter = new LECG.Core.SelectionFilters.SlabFilter();

        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElementRowViewModel.IsChecked)) RefreshCanRun();
        }

        private void RefreshCanRun()
        {
            OnPropertyChanged(nameof(CanRun));
            _applyCommandLocal?.NotifyCanExecuteChanged();
        }

        private void OnApply()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        private void OnCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }

        public void SetSelectedElements(IEnumerable<Element> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);
            List<Element> selectedElements = elements.Where(element => element != null).ToList();
            SelectedElementIds = selectedElements.Select(element => element.Id).Distinct().ToList();
            UpdateRowItems(selectedElements);
            Selection.UpdateSelection(SelectedElementIds.Count);
            RefreshCanRun();
        }

        public void InspectDocument(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            IsBusy = true;
            try
            {
                _inspectionById.Clear();
                var candidates = new FilteredElementCollector(doc)
                    .OfClass(typeof(Floor)).WhereElementIsNotElementType().Cast<Element>()
                    .Concat(new FilteredElementCollector(doc)
                        .OfClass(typeof(Toposolid)).WhereElementIsNotElementType().Cast<Element>())
                    .OrderBy(element => element.Id.Value)
                    .ToList();
                var eligible = new List<Element>();
                int unreadable = 0;
                foreach (Element element in candidates)
                {
                    SplitBoundaryInspection? inspection = TryInspectBoundaries(element);
                    if (inspection == null)
                    {
                        unreadable++;
                        continue;
                    }
                    _inspectionById[element.Id.Value] = inspection.Value;
                    if (inspection.Value.CanSplit) eligible.Add(element);
                }

                SetSelectedElements(eligible);
                InspectionStatus = eligible.Count == 0
                    ? $"Inspected {candidates.Count} elements. No splittable islands were found."
                    : $"Found {eligible.Count} eligible elements among {candidates.Count} inspected."
                        + (unreadable == 0 ? string.Empty : $" {unreadable} could not be inspected.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return RowItems.Where(row => row.IsChecked && _splittableIds.Contains(row.Id))
                .Select(row => doc.GetElement(new ElementId(row.Id)))
                .Where(element => element != null).ToList()!;
        }

        private void UpdateRowItems(IEnumerable<Element> elements)
        {
            foreach (ElementRowViewModel row in RowItems) row.PropertyChanged -= OnRowPropertyChanged;
            RowItems.Clear();
            _splittableIds.Clear();
            foreach (Element element in elements)
            {
                var (name, category) = ElementLabelService.GetLabels(element);
                SplitBoundaryInspection? inspection = TryInspectBoundaries(element);
                string status = inspection switch
                {
                    null => "Boundary count unavailable",
                    { BoundaryCount: <= 1 } => "No split needed",
                    { CanSplit: false } value => $"{value.BoundaryCount} boundaries, 1 island",
                    var value => $"Ready: {value.Value.BoundaryCount} loops / {value.Value.IslandCount} islands"
                };

                if (inspection?.CanSplit == true) _splittableIds.Add(element.Id.Value);

                var row = new ElementRowViewModel
                {
                    Id = element.Id.Value,
                    Name = name,
                    Category = category,
                    Type = element.GetType().Name,
                    Status = status,
                    IsChecked = true
                };
                row.PropertyChanged += OnRowPropertyChanged;
                RowItems.Add(row);
            }
        }

        private SplitBoundaryInspection? TryInspectBoundaries(Element element)
        {
            if (_inspectionById.TryGetValue(element.Id.Value, out SplitBoundaryInspection cached))
                return cached;
            try { return _splitBoundariesService.InspectBoundaries(element); }
            catch (Exception ex) when (IsExpectedSplitPreviewException(ex)) { return null; }
        }

        private static bool IsExpectedSplitPreviewException(Exception ex)
        {
            return ex is ArgumentException or InvalidOperationException or RevitExceptions.ArgumentException or RevitExceptions.InvalidOperationException;
        }
    }
}
