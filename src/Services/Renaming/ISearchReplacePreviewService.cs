using System.Collections.Generic;
using System.Threading;
using LECG.Models;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using LECG.Views;

namespace LECG.Services.Interfaces
{
    public interface ISearchReplacePreviewService
    {
        List<string> GetUniqueCategories(List<ElementData> elements);

        /// <summary>
        /// Processes the preview items based on criteria and context.
        /// Supports cancellation for real-time background execution.
        /// </summary>
        List<ElementRowViewModel> ProcessPreview(
            List<ElementData> candidates,
            SearchCriteria criteria,
            RenameRuleContext context,
            CancellationToken ct = default);
    }
}
