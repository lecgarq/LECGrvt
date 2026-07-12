using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    /// <summary>
    /// Service for background (silent) family modification.
    /// </summary>
    public interface IFamilyEditorService
    {
        /// <summary>
        /// Changes the category of a family in the background and reloads it.
        /// </summary>
        bool ChangeCategory(Family family, Category newCategory);

        /// <summary>
        /// Creates a new family by harvesting geometry from source and injecting into a new template of targetCategory.
        /// Returns the new family.
        /// </summary>
        Family RecreateAs(Family sourceFamily, Category targetCategory);
    }
}
