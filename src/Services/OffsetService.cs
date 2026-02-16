using System;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public class OffsetService : IOffsetService
    {
        public bool TryOffsetElement(Document doc, Element elem, double offset, Action<string>? logCallback = null)
        {
            try
            {
                Parameter? heightParam = elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM) 
                                         ?? elem.LookupParameter("Height Offset From Level");

                if (heightParam != null && !heightParam.IsReadOnly)
                {
                    double current = heightParam.AsDouble();
                    double newValue = current + offset;
                    heightParam.Set(newValue);

                    double oldDisplay = UnitUtils.ConvertFromInternalUnits(current, UnitTypeId.Meters);
                    double newDisplay = UnitUtils.ConvertFromInternalUnits(newValue, UnitTypeId.Meters);
                    
                    logCallback?.Invoke($"[{elem.Id}] {elem.Category.Name}: {oldDisplay:F3}m → {newDisplay:F3}m");
                    return true;
                }
                
                logCallback?.Invoke($"[{elem.Id}] ⚠ Parameter read-only or missing");
                return false;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"[{elem.Id}] ✗ Error: {ex.Message}");
                return false;
            }
        }
    }
}
