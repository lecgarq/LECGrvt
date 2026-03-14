using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISexyGraphicsApplyService
    {
        void Apply(
            IViewGraphicsFacade view,
            SexyRevitSettings settings,
            IProgressReporter reporter);
    }
}
