using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class OffsetService
    {
        public bool TryOffsetElement(Document doc, Element elem, double offset, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elem);

            try
            {
                Parameter? heightParam = elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                                         ?? elem.LookupParameter("Height Offset From Level");

                if (heightParam != null && !heightParam.IsReadOnly)
                {
                    // Convert offset from project display units to internal units (feet)
                    ForgeTypeId lengthUnit = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
                    double internalOffset = UnitUtils.ConvertToInternalUnits(offset, lengthUnit);

                    double current = heightParam.AsDouble();
                    double newValue = current + internalOffset;
                    heightParam.Set(newValue);

                    double oldDisplay = UnitUtils.ConvertFromInternalUnits(current, lengthUnit);
                    double newDisplay = UnitUtils.ConvertFromInternalUnits(newValue, lengthUnit);
                    string unitSuffix = FormatUnitSuffix(lengthUnit);

                    logCallback?.Invoke($"[{elem.Id}] {elem.Category.Name}: {oldDisplay:F3}{unitSuffix} → {newDisplay:F3}{unitSuffix}");
                    return true;
                }

                logCallback?.Invoke($"[{elem.Id}] ⚠ Parameter read-only or missing");
                return false;
            }
            catch (Exception ex) when (IsExpectedOffsetException(ex))
            {
                logCallback?.Invoke($"[{elem.Id}] ✗ Error: {ex.Message}");
                return false;
            }
        }

        private static string FormatUnitSuffix(ForgeTypeId unitTypeId)
        {
            if (unitTypeId == UnitTypeId.Meters) return "m";
            if (unitTypeId == UnitTypeId.Centimeters) return "cm";
            if (unitTypeId == UnitTypeId.Millimeters) return "mm";
            if (unitTypeId == UnitTypeId.Feet) return "ft";
            if (unitTypeId == UnitTypeId.Inches) return "in";
            return "";
        }

        private static bool IsExpectedOffsetException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
