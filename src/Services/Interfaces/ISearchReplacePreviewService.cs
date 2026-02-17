using System.Collections.Generic;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Services.Interfaces
{
    public interface ISearchReplacePreviewService
    {
        List<string> GetUniqueCategories(List<ElementData> elements);
        List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm);
    }
}
