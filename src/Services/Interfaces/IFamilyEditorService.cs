using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

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
        /// Performs a generic modification on a family document in the background and reloads it.
        /// </summary>
        bool ProcessFamily(Family family, Action<Document> action);

        /// <summary>
        /// Performs modifications on multiple families in a batch to optimize performance.
        /// </summary>
        void BatchProcess(IEnumerable<Family> families, Action<Document> action);

        /// <summary>
        /// Creates a new family by harvesting geometry from source and injecting into a new template of targetCategory.
        /// Returns the new family.
        /// </summary>
        Family RecreateAs(Family sourceFamily, Category targetCategory);
    }
}
