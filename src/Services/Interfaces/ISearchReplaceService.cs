using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Services.Interfaces
{
    public interface ISearchReplaceService
    {
        List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets);
        List<string> GetUniqueCategories(List<ElementData> elements);
        List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm);
        int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null);
    }
}
