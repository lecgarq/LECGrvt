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
                // Build a lookup: definition -> isInstance, using the document's ParameterBindings
                // This is the ONLY way to know if a param is Instance or Type in project context.
                var instanceDefs = new HashSet<Definition>();
                BindingMap bindingMap = doc.ParameterBindings;
                DefinitionBindingMapIterator it = bindingMap.ForwardIterator();
                while (it.MoveNext())
                {
                    if (it.Current is InstanceBinding)
                        instanceDefs.Add(it.Key);
                }

                // Group ALL FamilySymbols by their parent Family to ensure we collect
                // parameters from EVERY symbol, not just the first one encountered.
                FilteredElementCollector symbolCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .OfClass(typeof(FamilySymbol));

                // Group by Family.Id so we process each family once but scan ALL its symbols
                var symbolsByFamily = new Dictionary<long, List<FamilySymbol>>();
                foreach (FamilySymbol fs in symbolCollector)
                {
                    if (fs.Family == null) continue;
                    long familyId = fs.Family.Id.Value;
                    if (!symbolsByFamily.ContainsKey(familyId))
                        symbolsByFamily[familyId] = new List<FamilySymbol>();
                    symbolsByFamily[familyId].Add(fs);
                }

                foreach (var kvp in symbolsByFamily)
                {
                    List<FamilySymbol> symbols = kvp.Value;
                    string familyName = symbols[0].FamilyName;
                    long familyId = kvp.Key;

                    // Deduplicate parameters by Definition.Name across ALL symbols of this family
                    var seenParamNames = new HashSet<string>();

                    foreach (FamilySymbol fs in symbols)
                    {
                        foreach (Parameter p in fs.Parameters)
                        {
                             // Filter logic:
                             // - Must not be Shared (user req)
                             // - Must not be BuiltIn (Id < 0 is usually built-in)
                             // UPDATE: We allow ReadOnly because formula-driven parameters are ReadOnly
                             // in project, but their *definition* can still be renamed in the Family doc.

                             bool isShared = p.IsShared;
                             bool isBuiltIn = p.Id.Value < 0;

                             if (!isShared && !isBuiltIn && seenParamNames.Add(p.Definition.Name))
                             {
                                 // Determine instance/type from binding map
                                 bool isInstanceParam = instanceDefs.Contains(p.Definition);

                                 // Get param group label safely
                                 string paramGroupLabel = "";
                                 try
                                 {
                                     var groupTypeId = p.Definition.GetGroupTypeId();
                                     paramGroupLabel = LabelUtils.GetLabelForGroup(groupTypeId);
                                 }
                                 catch
                                 {
                                     paramGroupLabel = "";
                                 }

                                 data.Add(new ElementData
                                 {
                                     Id = familyId, // Store Family ID
                                     Name = p.Definition.Name, // Parameter Name
                                     Category = familyName, // Group by Family Name
                                     Type = "FamilyParameter",
                                     OriginalValue = p.Definition.Name,
                                     ParamGroup = paramGroupLabel,
                                     IsInstance = isInstanceParam,
                                     IsReadOnly = p.IsReadOnly
                                 });
                             }
                        }
                    }
                }
            }

            return data;
        }
    }
}
