#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertSharedCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            if (!doc.IsFamilyDocument)
            {
                message = "This tool can only be used in the Family Editor.";
                return Result.Cancelled;
            }

            try
            {
                Reference? pickedRef = null;
                try
                {
                    pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Select a Nested Family Instance to Unshare");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (pickedRef == null) return Result.Cancelled;
                
                Element? elem = doc.GetElement(pickedRef!);
                FamilyInstance? instance = elem as FamilyInstance;

                if (instance == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a Family Instance.");
                    return Result.Failed;
                }

                Family nestedFamily = instance!.Symbol.Family;
                
                using (Transaction t = new Transaction(doc, "Set Nested Family to Non-Shared"))
                {
                    t.Start();

                    Parameter? sharedParam = nestedFamily.LookupParameter("Shared");
                    
                    if (sharedParam != null)
                    {
                        if (sharedParam.IsReadOnly)
                        {
                            // Advanced: Edit the nested family document
                            t.RollBack(); 
                            
                            Document nestedDoc = doc.EditFamily(nestedFamily);
                            using (Transaction tNested = new Transaction(nestedDoc, "Uncheck Shared"))
                            {
                                tNested.Start();
                                Family? owner = nestedDoc.OwnerFamily;
                                Parameter? p = owner?.LookupParameter("Shared");
                                
                                if (p != null && !p.IsReadOnly)
                                {
                                    p.Set(0);
                                    tNested.Commit();
                                    
                                    // Load back without options (will prompt if needed, but unblocks build)
                                    nestedDoc.LoadFamily(doc);
                                    nestedDoc.Close(false);
                                    
                                    TaskDialog.Show("Success", $"Family '{nestedFamily.Name}' converted to Non-Shared.");
                                    return Result.Succeeded;
                                }
                                else
                                {
                                    tNested.RollBack();
                                    nestedDoc.Close(false);
                                    TaskDialog.Show("Error", "Could not modify Shared parameter in nested family.");
                                    return Result.Failed;
                                }
                            }
                        }
                        else
                        {
                            // Determine if we can set it directly
                            sharedParam.Set(0);
                            t.Commit();
                            TaskDialog.Show("Success", $"Family '{nestedFamily.Name}' converted to Non-Shared.");
                            return Result.Succeeded;
                        }
                    }
                    else
                    {
                        t.RollBack();
                        TaskDialog.Show("Error", "Could not find Shared parameter.");
                        return Result.Failed;
                    }
                }
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
