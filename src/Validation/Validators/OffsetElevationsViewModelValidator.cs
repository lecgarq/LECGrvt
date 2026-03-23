using FluentValidation;
using LECG.ViewModels;

namespace LECG.Validation.Validators
{
    public sealed class OffsetElevationsViewModelValidator : AbstractValidator<OffsetElevationsViewModel>
    {
        public OffsetElevationsViewModelValidator()
        {
            RuleFor(viewModel => viewModel.OffsetValue)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Offset must be zero or greater.");

            RuleFor(viewModel => viewModel.Selection.HasSelection)
                .Equal(true)
                .WithMessage("Select at least one floor or toposolid before running the command.");
        }
    }
}
