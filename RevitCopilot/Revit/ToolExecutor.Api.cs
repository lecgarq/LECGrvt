using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Autodesk.Revit.DB;
using LECG.RevitCopilot.Agent;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static object ExecuteApiProperty(Document doc, string operation, JsonElement args, bool change)
    {
        ApiPropertyBinding binding = RevitApiCatalog.Require(operation, change ? "change" : "read");
        Element[] targets = ReadElements(doc, args);
        foreach (Element target in targets)
        {
            if (!binding.Property.DeclaringType!.IsInstanceOfType(target))
                throw new ArgumentException($"{operation} requires {binding.Property.DeclaringType.FullName}; {target.Id.Value} is {target.GetType().FullName}.");
            if (change) ValidateMutable(doc, target);
        }
        object? value = change ? ConvertApiValue(doc, binding.Property.PropertyType, RequireProperty(args, "value"), OptionalString(args, "units")) : null;
        List<object> items = [];
        foreach (Element target in targets)
        {
            try
            {
                if (!change && TryGetUnsupportedApiRead(doc, operation, target) is { } unsupported)
                {
                    items.Add(new { unique_id = target.UniqueId, id = target.Id.Value, status = "unsupported", reason = unsupported });
                    continue;
                }
                object? before = SerializeApiValue(doc, binding.Property.GetValue(target));
                if (change) binding.Property.SetValue(target, value);
                items.Add(new { unique_id = target.UniqueId, id = target.Id.Value, before = change ? before : null,
                    value = change ? SerializeApiValue(doc, binding.Property.GetValue(target)) : before });
            }
            catch (TargetInvocationException ex)
            {
                if (!change && TryGetKnownApiApplicabilityError(operation, ex.InnerException?.Message ?? ex.Message) is { } reason)
                {
                    items.Add(new { unique_id = target.UniqueId, id = target.Id.Value, status = "unsupported", reason });
                    continue;
                }
                throw new InvalidOperationException($"{operation} on {target.Id.Value}: {ex.InnerException?.Message ?? ex.Message}", ex.InnerException ?? ex);
            }
        }
        return new { operation, value_type = binding.Property.PropertyType.FullName,
            units = "Revit API native units (lengths in feet, angles in radians; other values depend on the property).", items };
    }

    private static string? TryGetUnsupportedApiRead(Document doc, string operation, Element target)
    {
        if (operation is
            "api.get:Autodesk.Revit.DB.Family.CurtainPanelVerticalSpacing" or
            "api.get:Autodesk.Revit.DB.Family.CurtainPanelHorizontalSpacing" or
            "api.get:Autodesk.Revit.DB.Family.CurtainPanelTilePattern")
        {
            if (!doc.IsFamilyDocument || doc.OwnerFamily?.Id != target.Id)
                return "This Revit API property is only valid for the owner family inside a curtain-panel family document.";
        }

        if (operation == "api.get:Autodesk.Revit.DB.SunAndShadowSettings.Visible" && !target.ViewSpecific)
            return "This SunAndShadowSettings element is not view-specific, so Revit does not expose Visible for it.";

        if (operation == "api.get:Autodesk.Revit.DB.ViewSchedule.RowHeight" &&
            target is ViewSchedule schedule && schedule.RowHeightOverride == RowHeightOverrideOptions.None)
            return "Row height is available only when RowHeightOverride is All or ImageRows.";

        return ContextReadRestriction(operation, target);
    }

    private static string? TryGetKnownApiApplicabilityError(string operation, string message)
    {
        if (operation == "api.get:Autodesk.Revit.DB.Structure.FabricSheet.FabricHostReference")
            return "This FabricSheet does not expose a host reference in the current model context.";
        if (operation.StartsWith("api.get:Autodesk.Revit.DB.Family.CurtainPanel", StringComparison.Ordinal) &&
            (message.Contains("Curtain Panel", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("curtain panel", StringComparison.OrdinalIgnoreCase)))
            return "This family is not the owner curtain-panel family required by the Revit API.";
        if (operation == "api.get:Autodesk.Revit.DB.ViewSchedule.RowHeight" &&
            message.Contains("RowHeightOverrideOptions.None", StringComparison.OrdinalIgnoreCase))
            return "Row height is unavailable while RowHeightOverride is None.";
        return DocumentedReadFailure(operation, message);
    }

    private static object? SerializeApiValue(Document doc, object? value) => value switch
    {
        null => null,
        ElementId id => new { element_id = id.Value, unique_id = id.Value > 0 ? doc.GetElement(id)?.UniqueId : null },
        XYZ point => new { x = point.X, y = point.Y, z = point.Z },
        Color color => color.IsValid ? new { red = color.Red, green = color.Green, blue = color.Blue } : null,
        Enum enumeration => enumeration.ToString(),
        double number when !double.IsFinite(number) => throw new InvalidOperationException("API returned a non-finite number."),
        float number when !float.IsFinite(number) => throw new InvalidOperationException("API returned a non-finite number."),
        _ => value
    };

    private static object ConvertApiValue(Document doc, Type type, JsonElement value, string? units)
    {
        if ((type == typeof(double) || type == typeof(float) || type == typeof(XYZ)) && units != "revit_internal")
            throw new ArgumentException("Specify units='revit_internal' after checking the property's native units; never infer them from a numeric value.");
        if (type == typeof(string))
        {
            string text = value.GetString() ?? throw new ArgumentException("String value must not be null.");
            return text.Length <= 4000 ? text : throw new ArgumentException("String exceeds 4,000 characters.");
        }
        if (type == typeof(bool)) return value.GetBoolean();
        if (type == typeof(int)) return value.GetInt32();
        if (type == typeof(long)) return value.GetInt64();
        if (type == typeof(byte)) return value.GetByte();
        if (type == typeof(short)) return value.GetInt16();
        if (type == typeof(double) || type == typeof(float))
        {
            double number = value.GetDouble();
            if (!double.IsFinite(number) || (type == typeof(float) && Math.Abs(number) > float.MaxValue)) throw new ArgumentException("Number must be finite and within range.");
            return Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
        }
        if (type.IsEnum)
        {
            string text = value.GetString() ?? "";
            string[] names = text.Split(',', StringSplitOptions.TrimEntries);
            if (names.Length == 0 || names.Any(n => !Enum.GetNames(type).Contains(n, StringComparer.Ordinal)) ||
                (names.Length > 1 && !type.IsDefined(typeof(FlagsAttribute), false))) throw new ArgumentException("Use exact named enum values, not numbers.");
            return Enum.Parse(type, text);
        }
        if (type == typeof(ElementId))
        {
            if (value.TryGetProperty("unique_id", out var id)) return RequireElement(doc, id.GetString()!).Id;
            if (value.TryGetProperty("builtin_id", out var builtin) && builtin.GetInt64() < 0) return new ElementId(builtin.GetInt64());
            throw new ArgumentException("ElementId requires a current-document unique_id or a negative builtin_id sentinel.");
        }
        if (type == typeof(XYZ)) return new XYZ(Number(value, "x"), Number(value, "y"), Number(value, "z"));
        if (type == typeof(Color)) return new Color(Byte(value, "red"), Byte(value, "green"), Byte(value, "blue"));
        throw new ArgumentException($"Unsupported API value type {type.FullName}.");
    }
}
