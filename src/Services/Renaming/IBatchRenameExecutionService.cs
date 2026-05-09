using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.ViewModels.Components;

namespace LECG.Services.Interfaces
{
    public interface IBatchRenameExecutionService
    {
        int ExecuteBatchRename(Document doc, List<ElementRowViewModel> items, Logging.ILogger logger, IProgressReporter reporter);
        int ExecuteBatchRename(Document doc, List<ElementRowViewModel> items, Logging.ILogger logger, Action<double, string>? onProgress = null);
    }
}
