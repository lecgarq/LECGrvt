using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.Views.Base;
using System;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertSharedCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();

            if (!doc.IsFamilyDocument)
            {
                throw new InvalidOperationException("This tool can only be used in the Family Editor.");
            }

            Reference? pickedRef = null;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Select a Nested Family Instance to Unshare");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            if (pickedRef == null) return;

            Element? elem = doc.GetElement(pickedRef);
            FamilyInstance? instance = elem as FamilyInstance;

            if (instance == null)
            {
                throw new InvalidOperationException("Selected element is not a Family Instance.");
            }

            Family nestedFamily = instance.Symbol.Family;
            Parameter? sharedParam = nestedFamily.LookupParameter("Shared");

            if (sharedParam == null)
            {
                throw new InvalidOperationException("Could not find Shared parameter.");
            }

            if (sharedParam.IsReadOnly)
            {
                Document nestedDoc = doc.EditFamily(nestedFamily);

                try
                {
                    transactionService.Run(nestedDoc, "Uncheck Shared", _ =>
                    {
                        Family? owner = nestedDoc.OwnerFamily;
                        Parameter? nestedSharedParam = owner?.LookupParameter("Shared");

                        if (nestedSharedParam == null || nestedSharedParam.IsReadOnly)
                        {
                            throw new InvalidOperationException("Could not modify Shared parameter in nested family.");
                        }

                        nestedSharedParam.Set(0);
                    });

                    nestedDoc.LoadFamily(doc);
                    LecgDialog.Show("Success", $"Family '{nestedFamily.Name}' converted to Non-Shared.");
                }
                finally
                {
                    nestedDoc.Close(false);
                }

                return;
            }

            transactionService.Run(doc, "Set Nested Family to Non-Shared", _ => sharedParam.Set(0));
            LecgDialog.Show("Success", $"Family '{nestedFamily.Name}' converted to Non-Shared.");
        }
    }
}
