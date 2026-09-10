using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.ViewModels
{
    public partial class SubstanceCategoryViewModel : ObservableObject
    {
        public string Name { get; }
        public int Count { get; }
        public string Label => $"{Name} ({Count})";

        /// <summary>true = all, false = none, null = mixed.</summary>
        [ObservableProperty]
        private bool? _isChecked;

        private bool? _displayed;
        private bool _suppress;

        /// <summary>Invoked with (categoryName, selectAll) when the user clicks the box.</summary>
        public Action<string, bool>? CheckedByUser { get; set; }

        public SubstanceCategoryViewModel(string name, int count, bool? isChecked)
        {
            Name = name;
            Count = count;
            _isChecked = isChecked;
            _displayed = isChecked;
        }

        /// <summary>Updates the displayed state from the rows without treating it as a user click.</summary>
        public void SetFromRows(bool? state)
        {
            _suppress = true;
            IsChecked = state;
            _displayed = state;
            _suppress = false;
        }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (_suppress) return;

            // WPF's tri-state cycle is false -> true -> null -> false, so the new value is
            // not a reliable "what the user wants". Decide from what was displayed before:
            // a box that showed "all" clears the category; anything else selects it.
            bool selectAll = _displayed != true;
            _displayed = value;
            CheckedByUser?.Invoke(Name, selectAll);
        }
    }
}
