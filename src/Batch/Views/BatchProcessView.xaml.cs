using System.Text;
using System.Windows;
using System.Windows.Controls;
using LECG.Batch.ViewModels;
using LECG.Core;
using LECG.Services.Logging;

namespace LECG.Batch.Views
{
    public partial class BatchProcessView : LECG.Views.Base.LecgWindow
    {
        private readonly BatchProcessViewModel _vm;

        public BatchProcessView(BatchProcessViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            InitializeComponent();
            DataContext = vm;
            _vm = vm;

            // Auto-scroll log panel when new entries arrive
            _vm.LogEntries.CollectionChanged += (_, _) => ScrollLogToBottom();
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.Dispose();
            base.OnClosed(e);
        }

        private void OnBrowseModelsClick(object sender, RoutedEventArgs e)
        {
            var browser = ServiceLocator.GetRequiredService<CloudModelBrowserView>();
            browser.Owner = this;
            if (browser.ShowDialog() == true)
                _vm.AddJobsToQueue(browser.SelectedVersions);
        }

        // ── Log Panel ─────────────────────────────────────────────────────────

        private void OnLogToggled(object sender, RoutedEventArgs e)
        {
            bool isOpen = LogToggleButton.IsChecked == true;
            LogListView.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            LogToggleButton.Content = isOpen ? "▲ Log" : "▼ Log";

            if (isOpen)
                ScrollLogToBottom();
        }

        private void ScrollLogToBottom()
        {
            if (LogListView.Visibility != Visibility.Visible) return;
            if (LogListView.Items.Count == 0) return;

            Dispatcher.BeginInvoke(() =>
            {
                object last = LogListView.Items[LogListView.Items.Count - 1];
                LogListView.ScrollIntoView(last);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnCopyAllLog(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (LogEntry entry in _vm.LogEntries)
                sb.Append(entry.ToCopyText());

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch { }
        }

        private void OnCopyLogEntry(object sender, RoutedEventArgs e)
        {
            LogEntry? entry = null;

            if (sender is MenuItem menuItem)
            {
                // From ListView ContextMenu — use selected item
                entry = LogListView.SelectedItem as LogEntry;
            }

            if (entry == null) return;

            try
            {
                Clipboard.SetText(entry.ToCopyText());
            }
            catch { }
        }
    }
}
