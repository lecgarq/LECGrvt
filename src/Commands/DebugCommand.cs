using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DebugCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            var type = typeof(SlabShapeEditor);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Methods of SlabShapeEditor:");
            foreach (var m in methods)
            {
                sb.AppendLine($"- {m.Name} ({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
            }

            TaskDialog.Show("Debug API", sb.ToString());
        }
    }
}
