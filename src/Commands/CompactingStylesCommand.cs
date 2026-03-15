using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using LECG.Core;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Views.Base;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CompactingStylesCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();

            if (!ConfirmExecution())
            {
                return;
            }

            ShowLogWindow("Compacting Styles");

            Log("Compacting Styles");
            Log("=================");
            var reporter = new RevitCommandProgressReporter(Log, UpdateProgress);

            // Line Patterns
            var linePatternService = ServiceLocator.GetRequiredService<ILinePatternCompactionService>();
            transactionService.RunWithWarningHandler(doc, "Compacting Styles - Line Patterns", currentDoc =>
            {
                CompactingStylesContext context = CompactingStylesContext.Create(currentDoc, reporter);
                Stopwatch scopeTimer = Stopwatch.StartNew();
                LinePatternCompactionResult lpResult = linePatternService.Compact(currentDoc, context, reporter);
                scopeTimer.Stop();

                Log("");
                Log($"Line Patterns - Groups: {lpResult.DuplicateGroups}, Created: {lpResult.CanonicalPatternsCreated}, Rewired: {lpResult.ReferencesRewired}, Deleted: {lpResult.OriginalPatternsDeleted}, Blocked: {lpResult.BlockedDeletions.Count}");
                Log($"Line Patterns - Time: {scopeTimer.Elapsed.TotalSeconds:F2}s");
            });

            // Fill Patterns
            var fillPatternService = ServiceLocator.GetRequiredService<IFillPatternCompactionService>();
            transactionService.RunWithWarningHandler(doc, "Compacting Styles - Fill Patterns", currentDoc =>
            {
                CompactingStylesContext context = CompactingStylesContext.Create(currentDoc, reporter);
                Stopwatch scopeTimer = Stopwatch.StartNew();
                FillPatternCompactionResult fpResult = fillPatternService.Compact(currentDoc, context, reporter);
                scopeTimer.Stop();

                Log("");
                Log($"Fill Patterns - Groups: {fpResult.DuplicateGroups}, Created: {fpResult.CanonicalPatternsCreated}, Rewired: {fpResult.ReferencesRewired}, Deleted: {fpResult.OriginalPatternsDeleted}, Blocked: {fpResult.BlockedDeletions.Count}");
                Log($"Fill Patterns - Time: {scopeTimer.Elapsed.TotalSeconds:F2}s");
            });

            // Text Styles
            var textStyleService = ServiceLocator.GetRequiredService<ITextStyleCompactionService>();
            transactionService.RunWithWarningHandler(doc, "Compacting Styles - Text Styles", currentDoc =>
            {
                CompactingStylesContext context = CompactingStylesContext.Create(currentDoc, reporter);
                Stopwatch scopeTimer = Stopwatch.StartNew();
                TextStyleCompactionResult tsResult = textStyleService.Compact(currentDoc, context, reporter);
                scopeTimer.Stop();

                Log("");
                Log($"Text Styles - Groups: {tsResult.DuplicateGroups}, Created: {tsResult.CanonicalTypesCreated}, Rewired: {tsResult.ReferencesRewired}, Deleted: {tsResult.OriginalTypesDeleted}, Blocked: {tsResult.BlockedDeletions.Count}");
                Log($"Text Styles - Time: {scopeTimer.Elapsed.TotalSeconds:F2}s");
            });

            // Line Styles
            var lineStyleService = ServiceLocator.GetRequiredService<ILineStyleCompactionService>();
            transactionService.RunWithWarningHandler(doc, "Compacting Styles - Line Styles", currentDoc =>
            {
                CompactingStylesContext context = CompactingStylesContext.Create(currentDoc, reporter);
                Stopwatch scopeTimer = Stopwatch.StartNew();
                LineStyleCompactionResult lsResult = lineStyleService.Compact(currentDoc, context, reporter);
                scopeTimer.Stop();

                Log("");
                Log($"Line Styles - Groups: {lsResult.DuplicateGroups}, Rewired: {lsResult.ReferencesRewired}, Deleted: {lsResult.OriginalStylesDeleted}, Blocked: {lsResult.BlockedDeletions.Count}");
                Log($"Line Styles - Time: {scopeTimer.Elapsed.TotalSeconds:F2}s");
            });
        }

        private static bool ConfirmExecution()
        {
            return LecgDialog.Confirm(
                "Compacting Styles",
                "Normalize duplicated styles",
                "This command compacts Line Patterns, Fill Patterns, Text Styles, and Line Styles. For each scope it creates new canonical definitions, rewires reachable references, and deletes redundant originals when possible.");
        }
    }
}
