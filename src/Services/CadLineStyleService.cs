using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadLineStyleService : ICadLineStyleService
    {
        public GraphicsStyle CreateOrUpdateDetailLineStyle(Document familyDoc, string styleName, Color color, int weight)
        {
            Category detailCategory = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_DetailComponents);
            Category subCategory = detailCategory.SubCategories.Contains(styleName)
                ? detailCategory.SubCategories.get_Item(styleName)
                : familyDoc.Settings.Categories.NewSubcategory(detailCategory, styleName);

            subCategory.LineColor = color;
            subCategory.SetLineWeight(weight, GraphicsStyleType.Projection);

            return subCategory.GetGraphicsStyle(GraphicsStyleType.Projection);
        }
    }
}
