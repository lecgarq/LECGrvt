using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IFilterCopyService
    {
        Result<int> Apply(Document doc, IReadOnlyList<FilterCopyViewState> viewStates);
    }
}
