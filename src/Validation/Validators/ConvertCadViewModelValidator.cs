using System.IO;
using FluentValidation;
using LECG.ViewModels;

namespace LECG.Validation.Validators
{
    public sealed class ConvertCadViewModelValidator : AbstractValidator<ConvertCadViewModel>
    {
        public ConvertCadViewModelValidator()
        {
            RuleFor(viewModel => viewModel.NewFamilyName)
                .NotEmpty()
                .WithMessage("Enter a family name for the converted detail item.");

            RuleFor(viewModel => viewModel.TemplatePath)
                .NotEmpty()
                .WithMessage("Select a Revit family template.")
                .Must(File.Exists)
                .WithMessage("The selected Revit family template could not be found.");

            RuleFor(viewModel => viewModel.LineWeight)
                .InclusiveBetween(1, 16)
                .WithMessage("Line weight must be between 1 and 16.");

            When(viewModel => viewModel.UseSelectedImport, () =>
            {
                RuleFor(viewModel => viewModel.Selection.HasSelection)
                    .Equal(true)
                    .WithMessage("Select one imported CAD instance in the model.");
            });

            When(viewModel => !viewModel.UseSelectedImport, () =>
            {
                RuleFor(viewModel => viewModel.DwgFilePath)
                    .NotEmpty()
                    .WithMessage("Select a DWG file to convert.")
                    .Must(File.Exists)
                    .WithMessage("The selected DWG file could not be found.");
            });
        }
    }
}
