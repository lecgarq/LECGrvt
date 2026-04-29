using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using LECG.Models;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Services.Interfaces
{
    public interface ISearchReplaceService
    {
        List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets, bool materials, bool objectStyles, bool lineStyles, bool fillPatterns, bool familyParameters);
        List<string> GetUniqueCategories(List<ElementData> elements);

        /// <summary>
        /// Proxies the preview processing search logic. Supports cancellation.
        /// </summary>
        List<ReplaceItem> ProcessPreview(
            List<ElementData> candidates,
            SearchCriteria criteria,
            RenameRuleContext context,
            CancellationToken ct = default);

        int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, IProgressReporter reporter);
        int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null);
    }
}
