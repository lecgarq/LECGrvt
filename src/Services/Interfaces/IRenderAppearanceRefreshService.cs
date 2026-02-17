using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceRefreshService
    {
        void Refresh(Document doc, IList<Material> materials, Action<string>? logCallback = null);
    }
}
