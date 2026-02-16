using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class RenderAppearanceViewModel : BaseViewModel
    {
        public bool ShouldRun { get; private set; }

        public RenderAppearanceViewModel()
        {
            Title = "SYNC RENDER APPEARANCE";
        }

        public bool CanRun => true;

        [RelayCommand]
        private void Run()
        {
            ShouldRun = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void ExecuteCancel()
        {
            ShouldRun = false;
            CloseAction?.Invoke();
        }
    }
}
