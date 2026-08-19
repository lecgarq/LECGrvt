using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        /// Shift-click on the Sel column sets every row between the anchor and this one, in
        /// the grid's current visual order, to whatever this checkbox is about to become.
        /// A plain click just moves the anchor and falls through to normal handling.
        /// </summary>
        private void SelCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not CheckBox box || box.DataContext is not ElementRowViewModel row) return;
            if (DataContext is not SearchReplaceViewModel vm) return;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift || _rangeAnchor == null)
            {
                _rangeAnchor = row;
                return;
            }

            // The click has not been applied yet, so the target value is the negation of
            // where this row currently sits.
            vm.ToggleRange(_rangeAnchor, row, !row.IsChecked);
            _rangeAnchor = row;
            e.Handled = true;
        }
    }
}
