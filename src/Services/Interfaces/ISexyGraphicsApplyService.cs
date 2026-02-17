using System;
using LECG.ViewModels;

namespace LECG.Services.Interfaces
{
    public interface ISexyGraphicsApplyService
    {
        void Apply(
            IViewGraphicsFacade view,
            SexyRevitViewModel settings,
            Action<string> log,
            Action<double, string> progress);
    }
}
