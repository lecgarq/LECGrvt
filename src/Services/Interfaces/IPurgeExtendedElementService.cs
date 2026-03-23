using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeExtendedElementService
    {
        int PurgeUnusedGroups(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedGridTypes(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedLevelTypes(Document doc, Action<string>? logCallback = null);
        int PurgeConstraints(Document doc, Action<string>? logCallback = null);
        int PurgeUnplacedRooms(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedViewTemplates(Document doc, Action<string>? logCallback = null);
        int PurgeUnusedViewFilters(Document doc, Action<string>? logCallback = null);
    }
}
