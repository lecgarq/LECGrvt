using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.ViewModels
{
    public partial class SplitBoundariesViewModel : BaseViewModel
    {
        private readonly IGeometryBoundaryService _geometryBoundaryService;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<string> SelectedElementSummaries { get; } = new ObservableCollection<string>();

        public bool CanRun => Selection.HasSelection;

        private ICommand? _applyCommandLocal;
        public override ICommand ApplyCommand => _applyCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnApply, () => CanRun);

        private ICommand? _cancelCommandLocal;
        public override ICommand CancelCommand => _cancelCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnCancel);

        public SplitBoundariesViewModel(IGeometryBoundaryService geometryBoundaryService)
        {
            _geometryBoundaryService = geometryBoundaryService;
            Title = "SPLIT BOUNDARIES";
            Selection.ElementName = "Floors/Toposolids";
            Selection.Filter = new LECG.Core.SelectionFilters.SlabFilter();

            Selection.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectionViewModel.HasSelection))
                {
                    OnPropertyChanged(nameof(CanRun));
                }
            };
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
            Selection.UpdateSelection(SelectedElementIds.Count);
            UpdateSelectedElementSummaries(selectedElements);
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return SelectedElementIds.Select(doc.GetElement).Where(element => element != null).ToList()!;
        }

        private void UpdateSelectedElementSummaries(IEnumerable<Element> elements)
        {
            SelectedElementSummaries.Clear();
            foreach (Element element in elements)
            {
                int? boundaryCount = TryGetBoundaryCount(element);
                string boundaryLabel = boundaryCount.HasValue ? boundaryCount.Value.ToString() : "?";
                string status = boundaryCount switch { null => "Boundary count unavailable", <= 1 => "No split needed", _ => "Ready to split" };
                string category = element.Category?.Name ?? element.GetType().Name;
                SelectedElementSummaries.Add($"ID {element.Id} | {category} | Boundaries: {boundaryLabel} | {status}");
            }
        }

        private int? TryGetBoundaryCount(Element element)
        {
            try { return _geometryBoundaryService.ExtractLoops(element).Count; }
            catch (Exception ex) when (IsExpectedSplitPreviewException(ex)) { return null; }
        }

        private static bool IsExpectedSplitPreviewException(Exception ex)
        {
            return ex is ArgumentException or InvalidOperationException or RevitExceptions.ArgumentException or RevitExceptions.InvalidOperationException;
        }
    }
}
