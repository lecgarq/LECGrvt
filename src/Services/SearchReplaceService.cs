using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Configuration;
using LECG.Views;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;



namespace LECG.Services
{
    public class ElementData
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = ""; // "Type", "View", "Sheet"
    }

    public class SearchReplaceService : ISearchReplaceService
    {
        public SearchReplaceService()
        {
        }

        // 1. One-time Fetch of Grid Data
        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets)
        {
            List<ElementData> data = new List<ElementData>();
            
            // Types
            if (types)
            {
                 FilteredElementCollector typeCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType();
                 
                 foreach(var el in typeCollector)
                 {
                     if(el.Category == null) continue;
                     data.Add(new ElementData 
                     { 
                         Id = el.Id.Value, 
                         Name = el.Name, 
                         Category = el.Category.Name,
                         Type = "Type"
                     });
                 }
            }

            // Families
            if (families)
            {
                 FilteredElementCollector familyCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family));
                 
                 foreach(var el in familyCollector)
                 {
                     data.Add(new ElementData 
                     { 
                         Id = el.Id.Value, 
                         Name = el.Name, 
                         Category = "Families",
                         Type = "Family"
                     });
                 }
            }

            // Views & Sheets
            if (views || sheets)
            {
                 FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View));
                 
                 foreach(var el in viewCollector)
                 {
                     if(el is View v && !v.IsTemplate)
                     {
                         bool isSheet = v.ViewType == ViewType.DrawingSheet;
                         if(isSheet && sheets)
                         {
                             data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = "Sheets", Type = "Sheet" });
                         }
                         else if(!isSheet && views)
                         {
                             // Use ViewType for category or actual category? Views usually 'Views'
                             string cat = v.ViewType.ToString();
                             data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = cat, Type = "View" });
                         }
                     }
                 }
            }
            
            return data;
        }

        public List<string> GetUniqueCategories(List<ElementData> elements)
        {
            return elements.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        }

        // 2. pure Logic Transformation (Fast, In-Memory)
        public List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm)
        {
            List<ReplaceItem> results = new List<ReplaceItem>();

            foreach (var el in candidates)
            {
                // Filter by Scope (redundant if candidates pre-filtered, but safe)
                if (el.Type == "Type" && !vm.ScopeTypeName) continue;
                if (el.Type == "View" && !vm.ScopeViewName) continue;
                if (el.Type == "Sheet" && !vm.ScopeSheetName) continue;

                // Filter by Name
                if (!string.IsNullOrWhiteSpace(vm.FilterName))
                {
                    if (el.Name.IndexOf(vm.FilterName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                // Filter by Category (Exact Match from ComboBox usually, or Contains)
                if (!string.IsNullOrWhiteSpace(vm.FilterCategory) && !vm.FilterCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // If user selects specific category, we match exact or contains
                    // Assuming partial match for flexibility
                    if (el.Category.IndexOf(vm.FilterCategory, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                // Transformation
                string currentName = ApplyRules(el.Name, vm, results.Count); 
                
                // Add to results
                bool changed = !string.Equals(currentName, el.Name, StringComparison.Ordinal);
                
                results.Add(new ReplaceItem
                {
                    ElementId = el.Id,
                    ElementName = el.Name,
                    OriginalValue = el.Name,
                    NewValue = currentName,
                    IsChecked = true
                });
            }
            
            return results;
        }

        private string ApplyRules(string text, SearchReplaceViewModel vm, int index)
        {
            string result = text;

            // Pipeline
            // 1. Remove
            result = vm.RemoveRule.Apply(result, index);

            // 2. Replace
            result = vm.ReplaceRule.Apply(result, index);

            // 3. Case
            result = vm.CaseRule.Apply(result, index);

            // 4. Add
            result = vm.AddRule.Apply(result, index);

            // 5. Numbering
            result = vm.NumberingRule.Apply(result, index);

            return result;
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            int count = 0;
            int total = items.Count;
            int current = 0;

            logger.Log($"Starting batch rename for {total} items...");

            using (Transaction t = new Transaction(doc, "Batch Rename"))
            {
                t.Start();

                foreach (var item in items)
                {
                    current++;
                    double percent = (double)current / total * 100;
                    
                    if (!item.IsChecked) continue;
                    
                    ElementId id = new ElementId(item.ElementId); 
                    Element el = doc.GetElement(id);
                    
                    if (el != null)
                    {
                        try
                        {
                            onProgress?.Invoke(percent, $"Processing {item.ElementName}...");
                            
                            // Only rename if different
                            if(string.Equals(el.Name, item.NewValue, StringComparison.Ordinal)) continue;

                            el.Name = item.NewValue;
                            count++;
                            
                            // Detailed log only on actual change to avoid spam
                            logger.LogSuccess($"Renamed '{item.OriginalValue}' to '{item.NewValue}'");
                        }
                        catch (System.Exception ex)
                        {
                             logger.LogError($"ERROR renaming {item.ElementName}: {ex.Message}");
                        }
                    }
                }
                
                t.Commit();
            }

            logger.LogSuccess($"Batch rename complete. Modified {count} elements.");
            onProgress?.Invoke(100, "Done");
            
            return count;
        }
    }
}
