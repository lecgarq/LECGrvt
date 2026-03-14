using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    /// <summary>
    /// Base ViewModel for all LECG dialogs using CommunityToolkit.Mvvm.
    /// Manages standard Apply/Cancel commands manually to avoid source generator conflicts in complex inheritance.
    /// </summary>
    public abstract partial class BaseViewModel : ObservableObject
    {
        public Action? CloseAction { get; set; }

        [ObservableProperty]
        private string _title = "LECG Tool";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _shouldRun;

        private IRelayCommand? _applyCommand;
        public IRelayCommand ApplyCommand => _applyCommand ??= new RelayCommand(Apply);

        private IRelayCommand? _cancelCommand;
        public IRelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);

        public virtual void Apply()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        public virtual void Cancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
