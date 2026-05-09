using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using LECG.Services;

namespace LECG.ViewModels.Components
{
    public partial class SelectionViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _selectionCount;

        [ObservableProperty]
        private string _selectionStatus = "No items selected";

        [ObservableProperty]
        private bool _hasSelection;

        [ObservableProperty]
        private string _elementName = "Elements"; // e.g. "Walls", "Toposolids"

        public ISelectionFilter? Filter { get; set; }

        public event EventHandler? OnRequestSelect;

        /// <summary>
        /// Per-element rows surfaced in the selection grid (rendered by
        /// <c>ElementGridControl</c> embedded inside <c>SelectionControl</c>).
        /// Populated by <see cref="SetSelectionRows(System.Collections.Generic.IEnumerable{Autodesk.Revit.DB.Reference}, Autodesk.Revit.DB.Document)"/>
        /// or <see cref="SetSelectionRows(System.Collections.Generic.IEnumerable{Autodesk.Revit.DB.Element})"/>.
        /// </summary>
        public ObservableCollection<ElementRowViewModel> RowItems { get; } = new();

        public SelectionViewModel()
        {
        }

        public void UpdateSelection(int count)
        {
            SelectionCount = count;
            HasSelection = count > 0;
            SelectionStatus = count > 0
                ? $"{count} {ElementName} selected"
                : $"No {ElementName.ToLower()} selected";
        }

        /// <summary>
        /// Replaces <see cref="RowItems"/> with a row per resolved Reference.
        /// Skips refs that don't resolve to a live Element. Safe to call from
        /// the UI thread or a Revit event-handler thread (Dispatcher-marshalled).
        /// </summary>
        public void SetSelectionRows(IEnumerable<Reference>? refs, Document? doc)
        {
            InvokeOnUiThread(() =>
            {
                RowItems.Clear();
                if (refs == null || doc == null) return;
                foreach (Reference r in refs)
                {
                    Element? el = doc.GetElement(r);
                    if (el == null) continue;
                    RowItems.Add(BuildRow(el));
                }
            });
        }

        /// <summary>
        /// Replaces <see cref="RowItems"/> with a row per Element. Element-based
        /// overload for callers that already resolved Reference→Element (e.g.
        /// ChangeLevel, AlignElements with cross-doc refs).
        /// </summary>
        public void SetSelectionRows(IEnumerable<Element>? elements)
        {
            InvokeOnUiThread(() =>
            {
                RowItems.Clear();
                if (elements == null) return;
                foreach (Element? el in elements)
                {
                    if (el == null) continue;
                    RowItems.Add(BuildRow(el));
                }
            });
        }

        private static ElementRowViewModel BuildRow(Element el)
        {
            (string name, string category) = ElementLabelService.GetLabels(el);
            return new ElementRowViewModel
            {
                Id = el.Id.Value,
                Name = name,
                Category = category,
                Type = el.GetType().Name,
                Status = string.Empty,
                IsChecked = true
            };
        }

        private static void InvokeOnUiThread(Action action)
        {
            // RESEARCH §Pitfall 3: Revit selection events can fire from a
            // non-UI thread. Marshal to the WPF Dispatcher when available so
            // ObservableCollection mutations don't trip cross-thread asserts.
            Application? app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        [RelayCommand]
        private void Select()
        {
            OnRequestSelect?.Invoke(this, EventArgs.Empty);
        }
    }
}
