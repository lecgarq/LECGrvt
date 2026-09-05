using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;

namespace LECG.Commands
{
    /// <summary>Logs the appearance asset tree of a picked face and library schema names.</summary>
    [Transaction(TransactionMode.Manual)]
    public class DumpAppearanceAssetCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);
            ShowLogWindow("Appearance Asset Dump");
            AppearanceAssetDumpService.Run(uiDoc, doc, Log);
        }
    }
}
