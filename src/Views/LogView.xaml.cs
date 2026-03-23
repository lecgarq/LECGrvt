using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using LECG.Core;
using LECG.ViewModels;
using LECG.Services.Logging;

namespace LECG.Views
{
    public partial class LogView : Base.LecgWindow
    {
        private readonly LogViewModel _viewModel;

        public LogView(LogViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
            Closed += OnLogViewClosed;
        }

        public LogView()
            : this(ServiceLocator.GetService<LogViewModel>() ?? new LogViewModel(Logger.Instance))
        {
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (EntriesList.Items.Count == 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (EntriesList.Items.Count > 0)
                {
                    EntriesList.ScrollIntoView(EntriesList.Items[EntriesList.Items.Count - 1]);
                }
            }), DispatcherPriority.Background);
        }

        private void OnLogViewClosed(object? sender, EventArgs e)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
            Closed -= OnLogViewClosed;
        }
    }
}
