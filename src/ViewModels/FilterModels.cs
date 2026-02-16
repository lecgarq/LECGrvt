using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels
{
    public enum FilterStatus
    {
        Existing,   // Default state
        Created,    // Newly added (Green)
        Modified,   // Graphics update (Blue)
        Removable   // Marked for deletion (Red)
    }

    public partial class FilterItem : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private FilterStatus _status;

        public ElementId Id { get; set; }
        public OverrideGraphicSettings GraphicsSettings { get; set; }
        public bool IsVisibilityControlled { get; set; }
        [ObservableProperty]
        private bool _isVisible = true;

        public FilterItem(string name, ElementId id, OverrideGraphicSettings? graphics = null)
        {
            Name = name;
            Id = id;
            Status = FilterStatus.Existing;
            GraphicsSettings = graphics ?? new OverrideGraphicSettings();
        }
    }

    public partial class ViewContainer : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isExpanded;
        
        [ObservableProperty]
        private bool _isVisible = true;

        public ElementId Id { get; set; }
        public ViewType ViewType { get; set; }
        public bool IsTemplate { get; set; }

        public ObservableCollection<FilterItem> Filters { get; set; } = new ObservableCollection<FilterItem>();

        public ViewContainer(string name, ElementId id, bool isTemplate)
        {
            Name = name;
            Id = id;
            IsTemplate = isTemplate;
        }
    }
}
