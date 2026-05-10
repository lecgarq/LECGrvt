// Test fakes for SearchReplaceService facade delegation tests.
// Internal — accessible to LECG.Tests via InternalsVisibleTo.
// Defined in the LECG project so they can implement interfaces that reference
// Autodesk.Revit.DB.Document without requiring a direct RevitAPI.dll reference
// in the test project (Phase 04-03 pattern, extended for facade delegation).
using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using LECG.Views;

namespace LECG.Services.TestHelpers
{
    /// <summary>Test fake for IBaseElementCollectionService — records calls, returns configured result.</summary>
    internal sealed class FakeBaseElementCollectionService : IBaseElementCollectionService
    {
        public List<ElementData>? NextReturn { get; set; }

        public List<ElementData> CollectBaseElements(
            Document doc, bool types, bool families, bool views, bool sheets,
            bool materials, bool objectStyles, bool lineStyles, bool fillPatterns,
            bool familyParameters)
            => NextReturn!;
    }

    /// <summary>Test fake for ISearchReplacePreviewService — records calls, returns configured results.</summary>
    internal sealed class FakeSearchReplacePreviewService : ISearchReplacePreviewService
    {
        public List<string>? NextCategories { get; set; }
        public List<ElementRowViewModel>? NextPreview { get; set; }
        public CancellationToken LastToken { get; private set; }

        public List<string> GetUniqueCategories(List<ElementData> elements)
            => NextCategories!;

        public List<ElementRowViewModel> ProcessPreview(
            List<ElementData> candidates,
            SearchCriteria criteria,
            RenameRuleContext context,
            CancellationToken ct = default)
        {
            LastToken = ct;
            return NextPreview!;
        }
    }

    /// <summary>Test fake for IBatchRenameExecutionService — records calls, returns configured results.</summary>
    internal sealed class FakeBatchRenameExecutionService : IBatchRenameExecutionService
    {
        public int NextCount { get; set; }
        public IProgressReporter? LastReporter { get; private set; }
        public Action<double, string>? LastCallback { get; private set; }

        public int ExecuteBatchRename(
            Document doc,
            List<ElementRowViewModel> items,
            Logging.ILogger logger,
            IProgressReporter reporter)
        {
            LastReporter = reporter;
            return NextCount;
        }

        public int ExecuteBatchRename(
            Document doc,
            List<ElementRowViewModel> items,
            Logging.ILogger logger,
            Action<double, string>? onProgress = null)
        {
            LastCallback = onProgress;
            return NextCount;
        }
    }
}
