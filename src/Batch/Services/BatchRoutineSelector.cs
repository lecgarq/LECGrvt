using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;

namespace LECG.Batch.Services
{
    /// <summary>
    /// Singleton proxy registered as IBatchJobRoutine in the DI container.
    /// Holds all available routines; the ViewModel sets Selected at batch start time.
    /// The handler delegates Execute() to the selected routine.
    /// </summary>
    public class BatchRoutineSelector : IBatchJobRoutine
    {
        public IReadOnlyList<IBatchJobRoutine> Available { get; }

        public IBatchJobRoutine? Selected { get; set; }

        public string Name => ResolveSelected().Name;
        public string Description => ResolveSelected().Description;

        public BatchRoutineSelector(IReadOnlyList<IBatchJobRoutine> routines)
        {
            Available = routines;
            Selected = routines.FirstOrDefault();
        }

        public void Execute(UIApplication app, Document doc, BatchJob job)
            => ResolveSelected().Execute(app, doc, job);

        private IBatchJobRoutine ResolveSelected()
        {
            if (Selected != null)
            {
                return Selected;
            }

            IBatchJobRoutine? fallback = Available.FirstOrDefault();
            if (fallback != null)
            {
                Selected = fallback;
                return fallback;
            }

            throw new InvalidOperationException("No batch routines are registered.");
        }
    }
}
