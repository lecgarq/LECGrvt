using System.IO;
using LECG.Models;
using LECG.ViewModels;

namespace LECG.Validation
{
    public static class ValidationRules
    {
        public static bool TryValidate(object instance, out string message)
        {
            ArgumentNullException.ThrowIfNull(instance);

            var errors = new List<string>();
            switch (instance)
            {
                case WindowSettings settings when settings.IsInitialized:
                    if (!double.IsFinite(settings.Width) || settings.Width <= 0)
                        errors.Add("Window width must be a positive number.");
                    if (!double.IsFinite(settings.Height) || settings.Height <= 0)
                        errors.Add("Window height must be a positive number.");
                    break;

                case PurgeDialogSettings settings when !HasPurgeOperation(settings):
                    errors.Add("Select at least one purge target or enable Deep Purge.");
                    break;

                case OffsetElevationsViewModel viewModel:
                    if (!(viewModel.OffsetValue >= 0))
                        errors.Add("Offset must be zero or greater.");
                    if (!viewModel.Selection.HasSelection)
                        errors.Add("Select at least one floor or toposolid before running the command.");
                    break;

                case ConvertCadViewModel viewModel:
                    if (string.IsNullOrWhiteSpace(viewModel.NewFamilyName))
                        errors.Add("Enter a family name for the converted detail item.");
                    if (string.IsNullOrWhiteSpace(viewModel.TemplatePath))
                        errors.Add("Select a Revit family template.");
                    if (!File.Exists(viewModel.TemplatePath))
                        errors.Add("The selected Revit family template could not be found.");
                    if (viewModel.LineWeight is < 1 or > 16)
                        errors.Add("Line weight must be between 1 and 16.");
                    if (viewModel.UseSelectedImport && !viewModel.Selection.HasSelection)
                        errors.Add("Select one imported CAD instance in the model.");
                    if (!viewModel.UseSelectedImport && string.IsNullOrWhiteSpace(viewModel.DwgFilePath))
                        errors.Add("Select a DWG file to convert.");
                    if (!viewModel.UseSelectedImport && !File.Exists(viewModel.DwgFilePath))
                        errors.Add("The selected DWG file could not be found.");
                    break;
            }

            message = string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }

        private static bool HasPurgeOperation(PurgeDialogSettings settings)
        {
            return settings.IsDeepPurge
                || settings.PurgeLineStyles
                || settings.PurgeLinePatterns
                || settings.PurgeFillPatterns
                || settings.PurgeMaterials
                || settings.PurgeLevels
                || settings.PurgeParameters
                || settings.PurgeGroups
                || settings.PurgeGridTypes
                || settings.PurgeLevelTypes
                || settings.PurgeConstraints
                || settings.PurgeUnplacedRooms
                || settings.PurgeViewTemplates
                || settings.PurgeViewFilters;
        }
    }
}
