using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using LECG.ViewModels.Components;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.ViewModels
{
    public partial class DivideToposolidViewModel : BaseViewModel
    {
        public SelectionViewModel Selection { get; } = new SelectionViewModel();
        public List<ElementId> SelectedElementIds { get; private set; } = new List<ElementId>();
        public ObservableCollection<string> SelectedElementSummaries { get; } = new ObservableCollection<string>();

        public bool CanRun => Selection.HasSelection;

        private ICommand? _applyCommandLocal;
        public override ICommand ApplyCommand => _applyCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnApply, () => CanRun);

        private ICommand? _cancelCommandLocal;
        public override ICommand CancelCommand => _cancelCommandLocal ??= new CommunityToolkit.Mvvm.Input.RelayCommand(OnCancel);

        public DivideToposolidViewModel()
        {
            Title = "DIVIDE TOPOSOLID";
            Selection.ElementName = "Toposolids";
            Selection.Filter = new LECG.Core.SelectionFilters.ToposolidFilter();

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
                int? layerCount = TryGetLayerCount(element);
                string layerLabel = layerCount.HasValue ? layerCount.Value.ToString() : "?";
                string status = layerCount switch
                {
                    null => "Layer count unavailable",
                    <= 1 => "Already single layer",
                    _ => "Ready to divide"
                };
                string category = element.Category?.Name ?? element.GetType().Name;
                SelectedElementSummaries.Add($"ID {element.Id} | {category} | Layers: {layerLabel} | {status}");
            }
        }

        private static int? TryGetLayerCount(Element element)
        {
            try
            {
                var hostType = element.Document.GetElement(element.GetTypeId()) as HostObjAttributes;
                CompoundStructure? cs = hostType?.GetCompoundStructure();
                return cs?.GetLayers()?.Count;
            }
            catch (Exception ex) when (IsExpectedPreviewException(ex))
            {
                return null;
            }
        }

        private static bool IsExpectedPreviewException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
