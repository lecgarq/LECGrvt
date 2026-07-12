using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace LECG.Controls
{
    /// <summary>
    /// Shared per-row grid control for every plugin screen with a standard
    /// Sel | Type | Category | Name | Status column shape.
    /// Bind <see cref="RowItems"/> to an <see cref="IEnumerable"/> of
    /// <c>LECG.ViewModels.Components.ElementRowViewModel</c>.
    /// </summary>
    /// <remarks>
    /// Wraps <see cref="LecgDataGrid"/> so callers inherit virtualization and
    /// styling without
    /// re-stating column XAML. Per-screen extra columns (e.g. Original/New for
    /// Batch Rename) keep using <see cref="LecgDataGrid"/> directly — see
    /// 03-05-SUMMARY.md for the column-shape decision.
    /// </remarks>
    public partial class ElementGridControl : UserControl
    {
        public static readonly DependencyProperty RowItemsProperty = DependencyProperty.Register(
            nameof(RowItems), typeof(IEnumerable), typeof(ElementGridControl),
            new PropertyMetadata(null));

        public IEnumerable? RowItems
        {
            get => (IEnumerable?)GetValue(RowItemsProperty);
            set => SetValue(RowItemsProperty, value);
        }

        public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
            nameof(SelectionMode), typeof(DataGridSelectionMode), typeof(ElementGridControl),
            new PropertyMetadata(DataGridSelectionMode.Extended));

        public DataGridSelectionMode SelectionMode
        {
            get => (DataGridSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        // AdditionalColumns DP intentionally deferred — see SUMMARY.md.
        // Batch Rename keeps its own LecgDataGrid because Original/New columns
        // don't fit the shared shape; the migration sweep (Plans 03-06..08) will
        // revisit whether AdditionalColumns is worth carrying.

        public ElementGridControl()
        {
            InitializeComponent();
        }
    }
}
