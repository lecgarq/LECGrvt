using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class BaseElementCollectionService : IBaseElementCollectionService
    {
        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets, bool materials, bool objectStyles, bool lineStyles, bool fillPatterns, bool familyParameters)
        {
            List<ElementData> data = new List<ElementData>();

            if (types)
            {
                FilteredElementCollector typeCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType();

                foreach (var el in typeCollector)
                {
                    if (el.Category == null) continue;
                    data.Add(new ElementData
                    {
                        Id = el.Id.Value,
                        Name = el.Name,
                        Category = el.Category.Name,
                        Type = "Type",
                        OriginalValue = el.Name
                    });
                }
            }

            if (families)
            {
                FilteredElementCollector familyCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family));

                foreach (var el in familyCollector)
                {
                    data.Add(new ElementData
                    {
                        Id = el.Id.Value,
                        Name = el.Name,
                        Category = "Families",
                        Type = "Family",
                        OriginalValue = el.Name
                    });
                }
            }

            if (views || sheets)
            {
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View));

                foreach (var el in viewCollector)
                {
                    if (el is View v && !v.IsTemplate)
                    {
                        bool isSheet = v.ViewType == ViewType.DrawingSheet;
                        if (isSheet && sheets)
                        {
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = "Sheets", Type = "Sheet", OriginalValue = v.Name });
                        }
                        else if (!isSheet && views)
                        {
                            string cat = v.ViewType.ToString();
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = cat, Type = "View", OriginalValue = v.Name });
                        }
                    }
                }
            }

            if (materials)
            {
                FilteredElementCollector materialCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material));

                foreach (var el in materialCollector)
                {
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Materials", Type = "Material", OriginalValue = el.Name });
                }
            }

            if (fillPatterns)
            {
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                foreach (var el in patternCollector)
                {
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Fill Patterns", Type = "FillPattern", OriginalValue = el.Name });
                }
            }

            if (objectStyles || lineStyles)
            {
                FilteredElementCollector styleCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle));

                foreach (var el in styleCollector)
                {
                    if (el is GraphicsStyle gs && gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    {
                        Category cat = gs.GraphicsStyleCategory;
                        if (cat == null) continue;

                        // Identify if this is a built-in category/subcategory
                        // SAFE CHECK: BuiltInCategories have negative integer IDs.
                        // User-created subcategories have positive integer IDs.
                        bool isBuiltIn = cat.Id.Value < 0; 
                        
                        // We want to SHOW user created styles (which are not built-in).
                        // So if it IS built-in, we generally skip it...
                        // ...UNLESS it's an Import (which also has positive keys sometimes, but often negative if standard).
                        // Actually, Imports in Object Styles usually appear as subcategories of "Imports in Families".
                        
                        // For now, the user goal is to see styles that AREN'T showing up.
                        // The previous logic skipped if Enum.IsDefined, which might have been too aggressive 
                        // or coincidentally matching user IDs if they were large/small enough (unlikely but possible).
                        // The reliable check is IsBuiltIn -> Id < 0.

                        if (isBuiltIn) continue;

                        // Additional Check: If it is a subcategory of Lines, it is a Line Style
                        bool isLineStyle = cat.Parent != null && cat.Parent.Id.Value == (long)BuiltInCategory.OST_Lines;

                        if (isLineStyle && lineStyles)
                        {
                            data.Add(new ElementData 
                            { 
                                Id = el.Id.Value, 
                                Name = cat.Name, 
                                Category = "Line Styles", 
                                Type = "LineStyle",
                                OriginalValue = cat.Name
                            });
                        }
                        else if (!isLineStyle && objectStyles)
                        {
                            data.Add(new ElementData 
                            { 
                                Id = el.Id.Value, 
                                Name = cat.Name, 
                                Category = "Object Styles", 
                                Type = "ObjectStyle",
                                OriginalValue = cat.Name
                            });
                        }
                    }
                }
            }

            if (familyParameters)
            {
                // Track processed families to avoid processing same family multiple times via different types.
                HashSet<ElementId> processedFamilies = new HashSet<ElementId>();
                
                FilteredElementCollector symbolCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .OfClass(typeof(FamilySymbol));
                    
                foreach (FamilySymbol fs in symbolCollector)
                {
                    if (fs.Family == null || processedFamilies.Contains(fs.Family.Id)) continue;
                    
                    processedFamilies.Add(fs.Family.Id);
                    
                    foreach (Parameter p in fs.Parameters)
                    {
                         // Filter logic:
                         // - Must not be Shared (user req)
                         // - Must not be BuiltIn (Id > -1 is usually custom, but explicit check is safer)
                         // - Must not be ReadOnly (usually, though some formulas make it read only, but definition is what matters. Rename usually okay.)
                         
                         bool isShared = p.IsShared;
                         bool isBuiltIn = p.Id.Value < 0; 
                         
                         if (!isShared && !isBuiltIn && !p.IsReadOnly)
                         {
                             data.Add(new ElementData
                             {
                                 Id = fs.Family.Id.Value, // Store Family ID
                                 Name = p.Definition.Name, // Parameter Name
                                 Category = fs.FamilyName, // Group by Family Name
                                 Type = "FamilyParameter",
                                 OriginalValue = p.Definition.Name
                             });
                         }
                    }
                }
            }

            return data;
        }
    }
}
