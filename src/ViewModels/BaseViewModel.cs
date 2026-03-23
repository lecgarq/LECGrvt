using System;
using System.ComponentModel;
using System.Windows.Input;
using LECG.Core;
using LECG.Validation;
using LECG.Views.Base;

namespace LECG.ViewModels
{
    public abstract partial class BaseViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public Action? CloseAction { get; set; }

        private string _title = "LECG Tool";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _shouldRun;
        public bool ShouldRun { get => _shouldRun; set => SetProperty(ref _shouldRun, value); }

        private ICommand? _applyCommand;
        public virtual ICommand ApplyCommand => _applyCommand ??= new CommunityToolkit.Mvvm.Input.RelayCommand(Apply);

        private ICommand? _cancelCommand;
        public virtual ICommand CancelCommand => _cancelCommand ??= new CommunityToolkit.Mvvm.Input.RelayCommand(Cancel);

        public virtual void Apply()
        {
            if (!CanApply())
            {
                return;
            }

            ShouldRun = true;
            CloseAction?.Invoke();
        }

        public virtual void Cancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }

        protected bool CanApply()
        {
            IValidationService? validationService = ServiceLocator.GetService<IValidationService>();
            if (validationService == null || validationService.TryValidate(this, out string message))
            {
                return true;
            }

            string title = string.IsNullOrWhiteSpace(Title) ? "Validation" : $"{Title} Validation";
            LecgDialog.Show(title, message);
            return false;
        }
    }
}
