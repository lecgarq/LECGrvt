using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services.Logging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace LECG.ViewModels
{
    public partial class LogViewModel : ObservableObject
    {
        private readonly ILogger _logger;

        public ObservableCollection<LogEntry> Entries => _logger.Entries;

        [ObservableProperty]
        private string _title = "Operation Log";

        [ObservableProperty]
        private string _currentStatus = "Ready";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private bool _isBusy;

        public LogViewModel(ILogger logger)
        {
            _logger = logger ?? Logger.Instance;
            _logger.OnProgressUpdate += UpdateProgress;
        }

        [RelayCommand]
        private void Copy()
        {
            var sb = new StringBuilder();
            foreach(var entry in Entries)
            {
                sb.AppendLine($"[{entry.FormattedTime}] {entry.Level}: {entry.Message}");
            }
            
            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());
                // Could show a toast or small message here
                _logger.LogSuccess("Log copied to clipboard.");
            }
        }

        [RelayCommand]
        private void Clear()
        {
            _logger.Clear();
        }
        
        // Helper to update progress from commands
        public void UpdateProgress(double value, string status)
        {
            ProgressValue = value;
            CurrentStatus = status;
            // The logger's DoEvents will handle the UI refresh if called from the Logger.
            // If called directly here, we might need our own DoEvents if we aren't logging.
        }
    }
}
