using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using LECG.Core;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
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
            var reporter = new RevitCommandProgressReporter(Logger.Instance, UpdateProgress); // TEMPORARY: Wave 2
            var failureHandler = new SafeFailureHandler();

            // Line Patterns
            UpdateProgress(5, "Compacting Line Patterns...");
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
            }, failureHandler);

            // Fill Patterns
            UpdateProgress(25, "Compacting Fill Patterns...");
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
            }, failureHandler);

            // Text Styles
            UpdateProgress(50, "Compacting Text Styles...");
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
            }, failureHandler);

            // Line Styles
            UpdateProgress(75, "Compacting Line Styles...");
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
            }, failureHandler);

            UpdateProgress(100, "Complete");
            Log("");
            Log("=== COMPACTING COMPLETE ===");
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
