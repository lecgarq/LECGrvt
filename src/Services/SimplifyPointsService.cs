using Autodesk.Revit.DB;
using LECG.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class SimplifyPointsService : ISimplifyPointsService
    {
        public void SimplifyPoints(Document doc, IEnumerable<Element> elements, Action<double, string> progressCallback, Action<string> logCallback)
        {
            int successCount = 0;
            int totalPointsDeleted = 0;
            int current = 0;
            int total = elements.Count();

            using (Transaction t = new Transaction(doc, "Simplify Toposolid Points"))
            {
                t.Start();
                
                foreach (Element elem in elements)
                {
                    current++;
                    if (elem is Toposolid toposolid)
                    {
                        progressCallback?.Invoke((double)current / total * 100, $"Processing {elem.Id}...");
                        
                        SlabShapeEditor editor = toposolid.GetSlabShapeEditor();
                        if (editor != null)
                        {
                            if (!editor.IsEnabled) editor.Enable();

                            var vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                            if (vertices.Count > 0)
                            {
                                int initialCount = vertices.Count;
                                int deletedForThis = 0;
                                
                                foreach (var v in vertices)
                                {
                                    try
                                    {
                                        editor.DeletePoint(v);
                                        deletedForThis++;
                                        totalPointsDeleted++;
                                    }
                                    catch { }
                                }
                                
                                successCount++;
                                logCallback?.Invoke($"  ID {elem.Id}: Removed {deletedForThis} of {initialCount} points.");
                            }
                        }
                    }
                }

                t.Commit();
            }

            logCallback?.Invoke("");
            logCallback?.Invoke("=== SUMMARY ===");
            logCallback?.Invoke($"Toposolids processed: {successCount}");
            logCallback?.Invoke($"Total points removed: {totalPointsDeleted}");
        }
    }
}
