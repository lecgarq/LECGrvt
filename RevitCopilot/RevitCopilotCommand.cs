using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.RevitCopilot;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class RevitCopilotCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        ArgumentNullException.ThrowIfNull(commandData);
        try
        {
            if (RevitCopilotApp.Client is null || RevitCopilotApp.Dispatcher is null)
            {
                message = "The Revit Copilot did not initialize. Restart Revit and check the add-in load error.";
                return Result.Failed;
            }

            commandData.Application.GetDockablePane(RevitCopilotApp.PaneId).Show();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
