using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class SimplifyPointsService : ISimplifyPointsService
    {
        private readonly ITransactionService _transactionService;

        public SimplifyPointsService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public void SimplifyPoints(Document doc, IEnumerable<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            int successCount = 0;
            int totalPointsDeleted = 0;
            var elementList = elements.ToList();

            for (int i = 0; i < elementList.Count; i++)
            {
                Element elem = elementList[i];
                reporter.Report($"Processing {elem.Id}...", (double)(i + 1) / elementList.Count * 100);

                SlabShapeEditor? editor = GetEditor(elem);
                if (editor == null) continue;

                int deletedForThis = DeleteAllDeletablePoints(doc, elem, editor, reporter);
                totalPointsDeleted += deletedForThis;

                if (deletedForThis >= 0)
                {
                    successCount++;
                }
            }

            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            reporter.Log($"Elements processed: {successCount}");
            reporter.Log($"Total points removed: {totalPointsDeleted}");
        }

        private int DeleteAllDeletablePoints(Document doc, Element elem, SlabShapeEditor editor, IProgressReporter reporter)
        {
            int initialCount = CountVertices(editor);
            if (initialCount == 0) return 0;

            int totalDeleted = 0;

            _transactionService.Run(doc, $"Simplify Points - {elem.Id}", _ =>
            {
                if (!editor.IsEnabled) editor.Enable();

                // Multi-pass: after each pass that deletes points, vertex references
                // may become stale. Re-snapshot and retry until stable.
                int deletedThisPass;
                do
                {
                    deletedThisPass = 0;
                    var vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();

                    foreach (var v in vertices)
                    {
                        try
                        {
                            editor.DeletePoint(v);
                            deletedThisPass++;
                        }
                        catch (Exception ex) when (IsExpectedSimplifyPointsException(ex))
                        {
                            // Vertex protected by Revit — skip
                        }
                    }

                    totalDeleted += deletedThisPass;
                } while (deletedThisPass > 0);
            });

            int remaining = CountVertices(editor);
            reporter.Log($"  ID {elem.Id}: {initialCount} points -> {remaining} remaining ({totalDeleted} removed).");
            return totalDeleted;
        }

        private static SlabShapeEditor? GetEditor(Element elem)
        {
            if (elem is Toposolid t) return t.GetSlabShapeEditor();
            if (elem is Floor f) return f.GetSlabShapeEditor();
            return null;
        }

        private static int CountVertices(SlabShapeEditor editor)
        {
            return editor.SlabShapeVertices.Cast<SlabShapeVertex>().Count();
        }

        private static bool IsExpectedSimplifyPointsException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
