using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class SimplifyPointsService : ISimplifyPointsService
    {
        private readonly ITransactionService _transactionService;

        public SimplifyPointsService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public void SimplifyPoints(Document doc, IEnumerable<Element> elements, Action<double, string> progressCallback, Action<string> logCallback)
        {
            SimplifyPoints(doc, elements, new LegacyProgressReporter(progressCallback, logCallback));
        }

        public void SimplifyPoints(Document doc, IEnumerable<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int totalPointsDeleted = 0;
            int current = 0;
            int total = elements.Count();

            _transactionService.Run(doc, "Simplify Toposolid Points", currentDoc =>
            {
                foreach (Element elem in elements)
                {
                    current++;
                    if (elem is Toposolid toposolid)
                    {
                        reporter.Report($"Processing {elem.Id}...", (double)current / total * 100);
                        
                        SlabShapeEditor editor = toposolid.GetSlabShapeEditor();
                        if (editor != null)
                        {
                            if (!editor.IsEnabled) editor.Enable();

                            var vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                            if (vertices.Count > 0)
                            {
                                int initialCount = vertices.Count;
                                int deletedForThis = 0;
                                
                                foreach (var v in vertices)
                                {
                                    try
                                    {
                                        editor.DeletePoint(v);
                                        deletedForThis++;
                                        totalPointsDeleted++;
                                    }
                                    catch (Exception ex)
                                    {
                                        reporter.LogWarning($"  Warning: Failed to delete point in ID {elem.Id}: {ex.Message}");
                                    }
                                }
                                
                                successCount++;
                                reporter.Log($"  ID {elem.Id}: Removed {deletedForThis} of {initialCount} points.");
                            }
                        }
                    }
                }
            });

            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            reporter.Log($"Toposolids processed: {successCount}");
            reporter.Log($"Total points removed: {totalPointsDeleted}");
        }
    }
}
