using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;

namespace LECG.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class TestHarvestGeometryCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();
            var sel = uiDoc.Selection.GetElementIds();
            if(!sel.Any()) return;

            Element el = doc.GetElement(sel.First());
            int count = 0;

            transactionService.Run(doc, "Harvest Solid", _ =>
            {
                Options geomOptions = new Options() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geomElement = el.get_Geometry(geomOptions);
                if (geomElement != null)
                {
                    foreach (GeometryObject geomObj in geomElement)
                    {
                        if (geomObj is Solid solid && solid.Volume > 0)
                        {
                            FreeFormElement.Create(doc, solid);
                            count++;
                        }
                        else if (geomObj is GeometryInstance geomInst)
                        {
                            GeometryElement instGeom = geomInst.GetInstanceGeometry();
                            foreach (GeometryObject instObj in instGeom)
                            {
                                if (instObj is Solid solidInst && solidInst.Volume > 0)
                                {
                                    FreeFormElement.Create(doc, solidInst);
                                    count++;
                                }
                            }
                        }
                    }
                }
            });

            TaskDialog.Show("Result", $"Harvested {count} solids.");
        }
    }
}
