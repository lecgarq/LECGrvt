using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class BaseElementCollectionService : IBaseElementCollectionService
    {
        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets, bool materials, bool objectStyles, bool lineStyles, bool fillPatterns)
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
                        Type = "Type"
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
                        Type = "Family"
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
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = "Sheets", Type = "Sheet" });
                        }
                        else if (!isSheet && views)
                        {
                            string cat = v.ViewType.ToString();
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = cat, Type = "View" });
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
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Materials", Type = "Material" });
                }
            }

            if (fillPatterns)
            {
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                foreach (var el in patternCollector)
                {
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Fill Patterns", Type = "FillPattern" });
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

                        bool isLineStyle = cat.Parent != null && cat.Parent.Id.Value == (long)BuiltInCategory.OST_Lines;

                        if (isLineStyle && lineStyles)
                        {
                            data.Add(new ElementData { Id = el.Id.Value, Name = cat.Name, Category = "Line Styles", Type = "LineStyle" });
                        }
                        else if (!isLineStyle && objectStyles)
                        {
                            data.Add(new ElementData { Id = el.Id.Value, Name = cat.Name, Category = "Object Styles", Type = "ObjectStyle" });
                        }
                    }
                }
            }

            return data;
        }
    }
}
