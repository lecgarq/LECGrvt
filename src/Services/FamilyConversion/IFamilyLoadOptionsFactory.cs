using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyLoadOptionsFactory
    {
        /// <summary>
        /// Creates load options for LoadFamily. Pass <c>overwriteParameterValues = false</c>
        /// for definition-only edits (category change, purge, rename) so parameter values of
        /// already-placed instances and existing types are preserved; <c>true</c> (default)
        /// when the reloaded family is the source of truth (e.g. CAD re-conversion).
        /// </summary>
        IFamilyLoadOptions Create(bool overwriteParameterValues = true);
    }
}
