using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core.Substance;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface ISubstanceMaterialCreateService
    {
        HashSet<string> ExistingMaterialNames(Document doc);
        SubstanceMaterialReport Create(Document doc, SubstanceMaterialEntry entry, SubstanceBatchOptions options, Action<string>? log = null);
    }
}
