using LECG.Services.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace LECG.ViewModels
{
    public partial class LogViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, System.IDisposable
    {
        private readonly ILogger _logger;

        public ObservableCollection<LogEntry> Entries => _logger.Entries;

        private string _title = "Operation Log";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _currentStatus = "Ready";
        public string CurrentStatus { get => _currentStatus; set => SetProperty(ref _currentStatus, value); }

        private double _progressValue;
        public double ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }

        public ICommand CopyCommand { get; }

        public LogViewModel(ILogger logger)
        {
            _logger = logger ?? Logger.Instance;
            _logger.OnProgressUpdate += UpdateProgress;

            CopyCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Copy);
        }

        private void Copy()
        {
            var sb = new StringBuilder();
            foreach (var entry in Entries) sb.AppendLine($"[{entry.FormattedTime}] {entry.Level}: {entry.Message}");
            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());
                _logger.LogSuccess("Log copied to clipboard.");
            }
        }

        public void UpdateProgress(double value, string status)
        {
            ProgressValue = value;
            CurrentStatus = status;
        }

        public void Dispose()
        {
            if (_logger != null) _logger.OnProgressUpdate -= UpdateProgress;
            System.GC.SuppressFinalize(this);
        }
    }
}
