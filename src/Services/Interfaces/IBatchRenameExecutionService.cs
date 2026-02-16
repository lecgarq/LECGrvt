using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.ViewModels;

namespace LECG.Services.Interfaces
{
    public interface IBatchRenameExecutionService
    {
        int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Logging.ILogger logger, Action<double, string>? onProgress = null);
    }
}
