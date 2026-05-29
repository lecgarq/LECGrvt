using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassExecutionService
    {
        (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int groupsDeleted, int gridTypesDeleted, int levelTypesDeleted, int constraintsDeleted, int unplacedRoomsDeleted, int viewTemplatesDeleted, int viewFiltersDeleted) ExecutePass(
            Document doc,
            int passIndex,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool groups,
            bool gridTypes,
            bool levelTypes,
            bool constraints,
            bool unplacedRooms,
            bool viewTemplates,
            bool viewFilters,
            IProgressReporter reporter);
    }
}
