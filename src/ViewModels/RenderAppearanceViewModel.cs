using CommunityToolkit.Mvvm.ComponentModel;
using LECG.ViewModels.Components;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.ViewModels
{
    public partial class RenderAppearanceViewModel : BaseViewModel
    {
        public RenderAppearanceViewModel()
        {
            Title = "SYNC RENDER APPEARANCE";
        }

        public bool CanRun => true;
    }
}
