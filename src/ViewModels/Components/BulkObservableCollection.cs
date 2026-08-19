using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LECG.ViewModels.Components
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that can swap its whole contents while
    /// raising a single <see cref="NotifyCollectionChangedAction.Reset"/>.
    /// </summary>
    /// <remarks>
    /// Exists for the Batch Rename preview, which rebuilds the entire row set on every
    /// rule or filter change. Doing that as <c>Clear()</c> followed by one <c>Add</c> per
    /// row makes the bound <see cref="System.Windows.Data.ICollectionView"/> re-filter and
    /// re-sort once per row; at a few thousand rows that is the freeze. One Reset costs one
    /// re-evaluation.
    /// </remarks>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public BulkObservableCollection() { }

        public BulkObservableCollection(IEnumerable<T> items) : base(items) { }

        /// <summary>
        /// Replaces every element with <paramref name="items"/>, raising one Reset instead
        /// of one notification per element. Pass an empty sequence to clear.
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            CheckReentrancy();

            Items.Clear();
            if (items != null)
            {
                foreach (T item in items)
                {
                    Items.Add(item);
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
