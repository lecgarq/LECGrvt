using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.Models
{
    /// <summary>
    /// Represents a single element type group that can be selected for export.
    /// </summary>
    public partial class TypeGroupItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _typeName = "";

        [ObservableProperty]
        private int _elementCount;
    }
}
