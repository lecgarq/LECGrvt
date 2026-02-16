using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LECG.ViewModels
{
    /// <summary>
    /// Base ViewModel for all LECG dialogs using CommunityToolkit.Mvvm.
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        public Action? CloseAction { get; set; }

        [ObservableProperty]
        private string _title = "LECG Tool";

        [ObservableProperty]
        private bool _isBusy;

        [RelayCommand]
        protected virtual void Apply()
        {
            CloseAction?.Invoke();
        }

        [RelayCommand]
        protected virtual void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}
