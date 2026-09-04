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

        private bool _suppress;
        public Action<string, bool>? CheckedByUser { get; set; }

        public SubstanceCategoryViewModel(string name, int count, bool? isChecked)
        {
            Name = name;
            Count = count;
            _isChecked = isChecked;
        }

        public void SetFromRows(bool? state)
        {
            _suppress = true;
            IsChecked = state;
            _suppress = false;
        }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (_suppress) return;
            // A click on a mixed (null) box goes to true; on true goes to false.
            CheckedByUser?.Invoke(Name, value ?? true);
        }
    }
}
