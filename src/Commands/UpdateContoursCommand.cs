using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class UpdateContoursCommand : RevitCommand
    {
        protected override string? TransactionName => null; // Command handles own transaction

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var service = ServiceLocator.GetRequiredService<IToposolidService>();
            var vm = new UpdateContoursViewModel();
            
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

            var view = new UpdateContoursView(vm);
            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                int processed = 0;
                
                using (Transaction t = new Transaction(doc, "Update Contours"))
                {
                    t.Start();

                    foreach (var typeItem in vm.ToposolidTypes.Where(t => t.IsSelected))
                    {
                        try
                        {
                            service.UpdateContours(
                                doc, 
                                new ElementId(typeItem.ElementId), 
                                vm.EnablePrimary, 
                                vm.PrimaryInterval, 
                                vm.EnableSecondary, 
                                vm.SecondaryInterval, 
                                vm.IsApplyMode);
                            
                            processed++;
                        }
                        catch { }
                    }

                    t.Commit();
                }

                string mode = vm.IsApplyMode ? "Applied" : "Removed";
                TaskDialog.Show("Update Contours", $"{mode} contours on {processed} Toposolid type(s).");
            }
        }
    }
}
