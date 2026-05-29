using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeExecutionCoordinatorService
    {
        (int lineStylesDeleted, int linePatternsDeleted, int fillPatternsDeleted, int materialsDeleted, int levelsDeleted, int parametersDeleted, int groupsDeleted, int gridTypesDeleted, int levelTypesDeleted, int constraintsDeleted, int unplacedRoomsDeleted, int viewTemplatesDeleted, int viewFiltersDeleted) Execute(
            Document doc,
            int passCount,
            bool lineStyles,
            bool linePatterns,
            bool fillPatterns,
            bool materials,
            bool levels,
            bool parameters,
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
