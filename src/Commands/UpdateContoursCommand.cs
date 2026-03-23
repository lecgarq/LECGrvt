using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using LECG.Views.Base;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class UpdateContoursCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Command handles own transaction

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IToposolidService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();
            var view = ServiceLocator.GetRequiredService<UpdateContoursView>();
            var vm = (UpdateContoursViewModel)view.DataContext;

            // Populate ToposolidTypes from document
            var topoTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ToposolidType))
                .Cast<ToposolidType>()
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var type in topoTypes)
            {
                vm.ToposolidTypes.Add(new TypeSelectionItem
                {
                    Name = type.Name,
                    ElementId = type.Id.Value,
                    IsSelected = true
                });
            }
            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                int processed = 0;

                transactionService.Run(doc, "Update Contours", currentDoc =>
                {
                    foreach (var typeItem in vm.ToposolidTypes.Where(t => t.IsSelected))
                    {
                        try
                        {
                            service.UpdateContours(
                                currentDoc,
                                new ElementId(typeItem.ElementId),
                                vm.EnablePrimary,
                                vm.PrimaryInterval,
                                vm.EnableSecondary,
                                vm.SecondaryInterval,
                                vm.IsApplyMode);

                            processed++;
                        }
                        catch (Exception ex) when (IsExpectedUpdateContoursException(ex))
                        {
                            Log($"Failed to update contours for Toposolid type '{typeItem.Name}' ({typeItem.ElementId}): {ex.Message}");
                        }
                    }
                });

                string mode = vm.IsApplyMode ? "Applied" : "Removed";
                LecgDialog.Show("Update Contours", $"{mode} contours on {processed} Toposolid type(s).");
            }
        }

        private static bool IsExpectedUpdateContoursException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
