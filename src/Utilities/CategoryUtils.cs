using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Utilities
{
    public static class CategoryUtils
    {
        /// <summary>
        /// Gets all categories in the document that can be assigned to a loadable family.
        /// </summary>
        public static List<Category> GetValidFamilyCategories(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            var categories = doc.Settings.Categories;
            var result = new List<Category>();

            foreach (Category cat in categories)
            {
                if (cat == null) continue;

                // Model and Annotation categories are valid for loadable families
                if (cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.Annotation)
                {
                    result.Add(cat);
                }
            }

            return result.OrderBy(c => c.Name).ToList();
        }
    }
}
