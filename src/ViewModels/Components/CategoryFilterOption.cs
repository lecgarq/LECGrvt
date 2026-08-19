using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels.Components
{
    /// <summary>
    /// One entry in the Batch Rename category filter: a category name, how many preview rows
    /// carry it, and whether the user has ticked it.
    /// </summary>
    /// <remarks>
    /// The count is what makes the dropdown useful — picking a category blind tells you
    /// nothing about whether it will leave you with three rows or three thousand.
    /// </remarks>
    public partial class CategoryFilterOption : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public string Name { get; set; } = "";

        private int _count;
        public int Count { get => _count; set => SetProperty(ref _count, value); }
    }
}
