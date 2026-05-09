using Autodesk.Revit.DB;
using LECG.Services.Logging;

namespace LECG.Services;

/// <summary>
/// Centralized "give me a non-blank Name and Category for any Element" helper.
/// Enforces the no-blanks invariant in one place; eliminates per-VM inline DescribeElement formatters.
/// Locale-safe: uses BuiltInCategory + LabelUtils, not English category-name strings.
/// </summary>
public static class ElementLabelService
{
    /// <summary>
    /// Pure-string overload. Unit-testable without a Revit Element.
    /// </summary>
    /// <returns>
    /// Tuple of (name, category). Both are guaranteed non-null and non-whitespace.
    /// - rawName null/empty/whitespace -> "&lt;{clrTypeName} {id}&gt;"
    /// - rawName present -> trimmed
    /// - rawCategory null/empty/whitespace -> clrTypeName (or "Element" if clrTypeName is also blank)
    /// - rawCategory present -> trimmed
    /// </returns>
    public static (string name, string category) GetLabelsFromRaw(
        string rawName, string rawCategory, string clrTypeName, long id)
    {
        string safeClr = string.IsNullOrWhiteSpace(clrTypeName) ? "Element" : clrTypeName.Trim();

        string name = string.IsNullOrWhiteSpace(rawName)
            ? $"<{safeClr} {id}>"
            : rawName.Trim();

        string category = string.IsNullOrWhiteSpace(rawCategory)
            ? safeClr
            : rawCategory.Trim();

        return (name, category);
    }

    /// <summary>
    /// Revit-API overload. Resolves locale-safe Category label via BuiltInCategory + LabelUtils,
    /// with BuiltInParameter.ALL_MODEL_TYPE_NAME fallback for blank Name in non-English Revit.
    /// Logs a warning to LogView when any fallback fires.
    /// </summary>
    public static (string name, string category) GetLabels(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // 1) Resolve raw name. Fall back to BuiltInParameter.ALL_MODEL_TYPE_NAME for
        //    localized Revit installs where Element.Name may return blank for types.
        string rawName = element.Name;
        if (string.IsNullOrWhiteSpace(rawName))
        {
            try
            {
                var p = element.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME);
                rawName = p?.AsString() ?? string.Empty;
            }
            catch
            {
                rawName = string.Empty;
            }
        }

        // 2) Resolve raw category locale-safely via BuiltInCategory + LabelUtils.
        //    Explicit null-check on element.Category — do NOT use `element.Category?.Id.Value`
        //    cast chain; the `?.` short-circuits to a nullable long which mis-casts.
        string rawCategory = string.Empty;
        var cat = element.Category;
        if (cat != null)
        {
            try
            {
                var bic = (BuiltInCategory)cat.Id.Value;
                rawCategory = LabelUtils.GetLabelFor(bic) ?? string.Empty;
            }
            catch
            {
                rawCategory = cat.Name ?? string.Empty;
            }
        }

        string clrTypeName = element.GetType().Name;
        long id = element.Id.Value;

        var labels = GetLabelsFromRaw(rawName, rawCategory, clrTypeName, id);

        // 3) LogView warning when any fallback fired (raw category missing OR raw name missing).
        if (string.IsNullOrWhiteSpace(rawCategory) || string.IsNullOrWhiteSpace(element.Name))
        {
            try
            {
                Logger.Instance.LogWarning(
                    $"ElementLabelService: fallback for {clrTypeName} {id} -> ({labels.name}, {labels.category})");
            }
            catch
            {
                // Logger may be unavailable in some contexts (e.g., during early startup).
            }
        }

        return labels;
    }
}
