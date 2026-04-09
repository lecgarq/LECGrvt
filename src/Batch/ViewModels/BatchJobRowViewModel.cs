using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Batch.Models;

namespace LECG.Batch.ViewModels
{
    public partial class BatchJobRowViewModel : ObservableObject
    {
        private BatchJob _job;

        public BatchJobRowViewModel(BatchJob job)
        {
            _job = job;
        }

        public string JobId => _job.JobId;
        public string DisplayName => _job.DisplayName;
        public string ModelType => _job.ModelType.ToString();

        [ObservableProperty]
        private string _status = "";

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private int _retryCount;

        public void Update(BatchJob job)
        {
            _job = job;
            Status = job.Status.ToString();
            ErrorMessage = job.ErrorMessage;
            RetryCount = job.RetryCount;
        }
    }
}
