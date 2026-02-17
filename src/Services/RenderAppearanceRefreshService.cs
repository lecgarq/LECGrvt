using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceRefreshService : IRenderAppearanceRefreshService
    {
        public void Refresh(Document doc, IList<Material> materials, Action<string>? logCallback = null)
        {
            List<Material> materialsToToggle = new List<Material>();

            foreach (Material mat in materials)
            {
                if (mat.UseRenderAppearanceForShading)
                {
                    mat.UseRenderAppearanceForShading = false;
                    materialsToToggle.Add(mat);
                }
                else
                {
                    materialsToToggle.Add(mat);
                }
            }

            if (materialsToToggle.Count > 0)
            {
                doc.Regenerate();

                logCallback?.Invoke($"Forcing Render Appearance update on {materialsToToggle.Count} materials...");
                foreach (Material mat in materialsToToggle)
                {
                    mat.UseRenderAppearanceForShading = true;
                }

                doc.Regenerate();
            }
        }
    }
}
