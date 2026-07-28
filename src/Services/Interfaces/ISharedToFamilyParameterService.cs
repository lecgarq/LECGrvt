using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    /// <summary>
    /// Converts SHARED family parameters into ordinary (non-shared) FAMILY parameters,
    /// in bulk, while a project document is open. Each parameter keeps the exact same
    /// name, parameter group, data type, instance/type binding, formulas, and label
    /// associations — only its "is shared" property is dropped.
    /// </summary>
    /// <remarks>
    /// Families are reloaded into the project with <c>overwriteParameterValues = false</c>
    /// so that per-instance and per-type values already placed in the project are
    /// preserved. (The Category Changer reload path uses the default factory which sets
    /// this to <c>true</c>, which is why it can reset placed instance values — this tool
    /// deliberately avoids that.)
    /// </remarks>
    public interface ISharedToFamilyParameterService
    {
        /// <summary>
        /// Converts every shared parameter to a non-shared family parameter for each of
        /// the supplied families, reloading them into <paramref name="projectDoc"/>.
        /// </summary>
        /// <param name="projectDoc">The open project document the families belong to.</param>
        /// <param name="families">Families to process (duplicates and non-editable families are skipped).</param>
        /// <returns>A summary of what happened.</returns>
        SharedToFamilyParameterResult ConvertFamilies(Document projectDoc, IEnumerable<Family> families);
    }

    /// <summary>
    /// Aggregate outcome of a bulk shared-to-family parameter conversion.
    /// </summary>
    public sealed class SharedToFamilyParameterResult
    {
        /// <summary>Families that were opened in the family editor and inspected.</summary>
        public int FamiliesProcessed { get; set; }

        /// <summary>Families that had at least one shared parameter converted and were reloaded.</summary>
        public int FamiliesModified { get; set; }

        /// <summary>Families skipped because they were not editable or had no shared parameters.</summary>
        public int FamiliesSkipped { get; set; }

        /// <summary>Families that threw an unexpected error during processing.</summary>
        public int FamiliesFailed { get; set; }

        /// <summary>Total individual shared parameters converted across all families.</summary>
        public int ParametersConverted { get; set; }
    }
}
