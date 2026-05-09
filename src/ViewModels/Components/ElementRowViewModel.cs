using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels.Components
{
    /// <summary>
    /// Shared per-row data model for every grid in the plugin.
    /// Replaces the legacy <c>ReplaceItem</c> and ad-hoc
    /// <c>ObservableCollection&lt;string&gt;</c> summaries used across 7+ ViewModels.
    /// </summary>
    /// <remarks>
    /// Inherits <see cref="ObservableObject"/> so the WPF DataGrid (LecgDataGrid)
    /// can drive Select All / Select None via reflection on <c>IsChecked</c>.
    /// Only <c>IsChecked</c> is observable — identity / display fields are set once
    /// at row creation and need no change-notification churn.
    /// </remarks>
    public partial class ElementRowViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isChecked = true;

        // Identity & display (no-blanks invariant — guaranteed non-blank by ElementLabelService)
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = "";        // discriminator: "Type"|"Family"|"FamilyParameter"|"View"|"Sheet"|...
        public string Status { get; set; } = "";      // command-specific outcome / skip reason
        public string Family { get; set; } = "";      // populated for FamilyParameter rows

        // Batch Rename carry-over (used only by SearchReplace path)
        public string OriginalValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string ParamGroup { get; set; } = "";
        public bool IsInstance { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
