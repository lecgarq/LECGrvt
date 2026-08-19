using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using LECG.Views.Base;

namespace LECG.Views
{
    public partial class SearchReplaceView : LecgWindow
    {
        public SearchReplaceView(SearchReplaceViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            BindDialogClose(viewModel, () => viewModel.ShouldRun);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>Last Sel checkbox clicked without Shift — the anchor for a range click.</summary>
        private ElementRowViewModel? _rangeAnchor;

        /// <summary>
        /// Ticking a box applies to the whole row selection, not just the row you hit.
        /// </summary>
        /// <remarks>
        /// Three gestures, in priority order:
        /// 1. The clicked row is part of a multi-row selection — every selected row follows it.
        ///    This is the one people actually reach for: select rows, click a box.
        /// 2. Shift is held — everything between the last plain click and this one follows it.
        /// 3. Plain click — normal single toggle; just moves the anchor.
        ///
        /// This is PreviewMouseLeftButtonDown on purpose. The DataGrid collapses the selection
        /// to the clicked row as part of handling the click, so by the time a Click handler
        /// runs, SelectedItems is already down to one and the batch is gone.
        /// </remarks>
        private void SelCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not CheckBox box || box.DataContext is not ElementRowViewModel row) return;
            if (DataContext is not SearchReplaceViewModel vm) return;

            bool target = !row.IsChecked;

            List<ElementRowViewModel> selection = FindAncestor<DataGrid>(box)?.SelectedItems
                .OfType<ElementRowViewModel>()
                .ToList() ?? new List<ElementRowViewModel>();

            if (selection.Count > 1 && selection.Contains(row))
            {
                vm.BulkSetChecked(selection, _ => target);
                _rangeAnchor = row;
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _rangeAnchor != null)
            {
                vm.ToggleRange(_rangeAnchor, row, target);
                _rangeAnchor = row;
                e.Handled = true;
                return;
            }

            _rangeAnchor = row;
        }

        /// <summary>
        /// Space toggles every selected row — the keyboard half of the same gesture.
        /// </summary>
        private void PreviewGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;
            if (sender is not DataGrid grid) return;
            if (DataContext is not SearchReplaceViewModel vm) return;

            List<ElementRowViewModel> selection = grid.SelectedItems.OfType<ElementRowViewModel>().ToList();
            if (selection.Count == 0) return;

            // Anchor off the first selected row so a mixed selection resolves one way, not
            // per-row — otherwise Space scrambles the selection instead of setting it.
            bool target = !selection[0].IsChecked;
            vm.BulkSetChecked(selection, _ => target);
            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            DependencyObject? current = start;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
