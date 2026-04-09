using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;

namespace LECG.Batch.Services
{
    /// <summary>
    /// No-op default routine. Replace with a custom IBatchJobRoutine implementation
    /// and register it in the DI container to add project-specific business logic.
    /// </summary>
    public class DefaultBatchJobRoutine : IBatchJobRoutine
    {
        public void Execute(UIApplication app, Document doc, BatchJob job)
        {
            // No-op: override by registering a custom IBatchJobRoutine in Bootstrapper
        }
    }
}
