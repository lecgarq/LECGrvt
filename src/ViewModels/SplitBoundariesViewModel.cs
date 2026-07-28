using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels.Components;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.ViewModels
{
    public partial class SplitBoundariesViewModel : BaseViewModel
    {
        private readonly GeometryBoundaryService _geometryBoundaryService;

        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<ElementRowViewModel> RowItems { get; } = new ObservableCollection<ElementRowViewModel>();

        public bool CanRun => Selection.HasSelection;

        private ICommand? _applyCommandLocal;
        public override ICommand ApplyCommand => _applyCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnApply, () => CanRun);

        private ICommand? _cancelCommandLocal;
        public override ICommand CancelCommand => _cancelCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnCancel);

        public SplitBoundariesViewModel(GeometryBoundaryService geometryBoundaryService)
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
            UpdateRowItems(selectedElements);
        }

        public List<Element> GetSelectedElements(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);
            return SelectedElementIds.Select(doc.GetElement).Where(element => element != null).ToList()!;
        }

        private void UpdateRowItems(IEnumerable<Element> elements)
        {
            RowItems.Clear();
            foreach (Element element in elements)
            {
                var (name, category) = ElementLabelService.GetLabels(element);
                int? boundaryCount = TryGetBoundaryCount(element);
                string status = boundaryCount switch
                {
                    null => "Boundary count unavailable",
                    <= 1 => "No split needed",
                    _ => $"Ready to split ({boundaryCount.Value} boundaries)"
                };

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
