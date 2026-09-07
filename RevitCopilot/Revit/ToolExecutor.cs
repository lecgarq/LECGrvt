using System.Collections;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LECG.RevitCopilot.Revit;

internal sealed partial class ToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal string Execute(UIApplication app, string toolName, string argumentsJson)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using JsonDocument arguments = ParseArguments(argumentsJson);
            if (toolName.StartsWith("agent_recipe_", StringComparison.Ordinal))
                return JsonSerializer.Serialize(new { success = true, data = RunLibraryTool(toolName, arguments.RootElement) }, JsonOptions);
            UIDocument uiDoc = app.ActiveUIDocument
                ?? throw new InvalidOperationException("No Revit project is currently active.");
            Document doc = uiDoc.Document;

            object result = toolName switch
            {
                "project_info" => ProjectInfo(uiDoc, doc),
                "elements_query" => ElementsQuery(doc, arguments.RootElement),
                "element_get" => ElementGet(doc, arguments.RootElement),
                "element_set_parameter" => ElementSetParameter(doc, arguments.RootElement),
                "elements_delete" => ElementsDelete(doc, arguments.RootElement),
                "view_isolate_or_select" => ViewSelect(uiDoc, arguments.RootElement),
                "__current_selection" => CurrentSelection(uiDoc),
                "agent_read" => RunAgentRead(uiDoc, doc, arguments.RootElement),
                "agent_read_batch" => ReadBatch(uiDoc, doc, arguments.RootElement),
                "agent_preview_batch" => PreviewChange(doc, LECG.RevitCopilot.Agent.AgentBatch.ChangeOperation, arguments.RootElement),
                "agent_preview" => RunAgentPreview(doc, arguments.RootElement),
                "agent_apply" => ApplyChange(doc, RequireString(arguments.RootElement, "preview_id")),
                _ => throw new ArgumentException($"Unknown tool '{toolName}'.")
            };

            return JsonSerializer.Serialize(new { success = true, execution_ms = timer.Elapsed.TotalMilliseconds, data = result }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new { success = false, tool = toolName, error = ex.Message, execution_ms = timer.Elapsed.TotalMilliseconds },
                JsonOptions);
        }
    }

    private static object ProjectInfo(UIDocument uiDoc, Document doc)
    {
        object[] levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(level => level.Elevation)
            .Select(level => new
            {
                id = level.Id.Value,
                level.Name,
                elevation_internal_feet = level.Elevation,
                elevation_display = FormatLength(doc, level.Elevation)
            })
            .Cast<object>()
            .ToArray();

        return new
        {
            doc.Title,
            file_path = string.IsNullOrWhiteSpace(doc.PathName) ? null : doc.PathName,
            active_view = uiDoc.ActiveView?.Name,
            is_workshared = doc.IsWorkshared,
            levels
        };
    }

    private static object ElementsQuery(Document doc, JsonElement args)
    {
        string categoryName = RequireString(args, "category");
        if (!Enum.TryParse(categoryName, ignoreCase: true, out BuiltInCategory category))
        {
            throw new ArgumentException($"Unknown BuiltInCategory '{categoryName}'. Use a name such as OST_Walls.");
        }

        int limit = Math.Clamp(OptionalInt(args, "limit") ?? 50, 1, 100);
        string? requestedLevel = OptionalString(args, "level");

        IEnumerable<Element> query = new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .Cast<Element>();

        if (!string.IsNullOrWhiteSpace(requestedLevel))
        {
            query = query.Where(element => string.Equals(
                GetElementLevelName(doc, element),
                requestedLevel,
                StringComparison.OrdinalIgnoreCase));
        }

        List<Element> matches = query.ToList();
        object[] returned = matches.Take(limit).Select(element => SummarizeElement(doc, element)).ToArray();

        return new
        {
            total_matches = matches.Count,
            returned_count = returned.Length,
            is_truncated = matches.Count > returned.Length,
            elements = returned
        };
    }

    private static object ElementGet(Document doc, JsonElement args)
    {
        string uniqueId = RequireString(args, "unique_id");
        Element element = doc.GetElement(uniqueId)
            ?? throw new ArgumentException($"No element with unique_id '{uniqueId}' exists in the active document.");

        object[] parameters = element.Parameters
            .Cast<Parameter>()
            .OrderBy(parameter => parameter.Definition?.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(parameter => SerializeParameter(doc, parameter))
            .ToArray();

        return new
        {
            id = element.Id.Value,
            unique_id = element.UniqueId,
            name = SafeElementName(element),
            category = element.Category?.Name,
            parameters
        };
    }

    private static object ElementSetParameter(Document doc, JsonElement args)
    {
        string uniqueId = RequireString(args, "unique_id");
        string parameterName = RequireString(args, "parameter_name");
        JsonElement newValue = RequireProperty(args, "new_value");

        Element element = doc.GetElement(uniqueId)
            ?? throw new ArgumentException($"No element with unique_id '{uniqueId}' exists in the active document.");
        ValidateMutable(doc, element);

        Parameter parameter = FindParameter(element, parameterName)
            ?? throw new ArgumentException($"Parameter '{parameterName}' was not found on element {element.Id.Value}.");
        if (parameter.IsReadOnly)
        {
            throw new InvalidOperationException($"Parameter '{parameterName}' is read-only.");
        }

        object oldValue = SerializeParameter(doc, parameter);
        using Transaction transaction = new(doc, $"Copilot: Set {parameterName}");
        bool started = false;
        try
        {
            TransactionStatus startStatus = transaction.Start();
            started = startStatus == TransactionStatus.Started;
            if (!started) throw new InvalidOperationException($"Transaction could not start: {startStatus}.");

            bool changed = SetParameterValue(doc, parameter, newValue);
            if (!changed) throw new InvalidOperationException("Revit rejected the parameter value.");

            TransactionStatus commitStatus = transaction.Commit();
            started = false;
            if (commitStatus != TransactionStatus.Committed)
            {
                throw new InvalidOperationException($"Transaction did not commit: {commitStatus}.");
            }
        }
        catch
        {
            if (started && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
            throw;
        }

        return new
        {
            status = "committed",
            element_id = element.Id.Value,
            unique_id = element.UniqueId,
            parameter_name = parameter.Definition.Name,
            old_value = oldValue,
            new_value = SerializeParameter(doc, parameter)
        };
    }

    private static object ElementsDelete(Document doc, JsonElement args)
    {
        JsonElement values = RequireProperty(args, "unique_ids");
        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("'unique_ids' must be an array of strings.");
        }

        string[] uniqueIds = values.EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (uniqueIds.Length == 0) throw new ArgumentException("At least one unique_id is required.");

        List<Element> elements = new(uniqueIds.Length);
        foreach (string uniqueId in uniqueIds)
        {
            Element element = doc.GetElement(uniqueId)
                ?? throw new ArgumentException($"No element with unique_id '{uniqueId}' exists in the active document.");
            ValidateMutable(doc, element);
            elements.Add(element);
        }

        ICollection<ElementId> deleted;
        using Transaction transaction = new(doc, "Copilot: Delete elements");
        bool started = false;
        try
        {
            TransactionStatus startStatus = transaction.Start();
            started = startStatus == TransactionStatus.Started;
            if (!started) throw new InvalidOperationException($"Transaction could not start: {startStatus}.");

            deleted = doc.Delete(elements.Select(element => element.Id).ToList());
            TransactionStatus commitStatus = transaction.Commit();
            started = false;
            if (commitStatus != TransactionStatus.Committed)
            {
                throw new InvalidOperationException($"Transaction did not commit: {commitStatus}.");
            }
        }
        catch
        {
            if (started && transaction.GetStatus() == TransactionStatus.Started)
            {
                transaction.RollBack();
            }
            throw;
        }

        return new
        {
            status = "committed",
            requested_count = elements.Count,
            deleted_count = deleted.Count,
            deleted_ids = deleted.Select(id => id.Value).ToArray()
        };
    }

    private static object ViewSelect(UIDocument uiDoc, JsonElement args)
    {
        JsonElement values = RequireProperty(args, "element_ids");
        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("'element_ids' must be an array of integers.");
        }

        List<ElementId> ids = values.EnumerateArray()
            .Select(ReadElementId)
            .DistinctBy(id => id.Value)
            .ToList();
        if (ids.Count == 0) throw new ArgumentException("At least one element_id is required.");

        long[] missing = ids.Where(id => uiDoc.Document.GetElement(id) is null).Select(id => id.Value).ToArray();
        if (missing.Length > 0)
        {
            throw new ArgumentException($"These element IDs do not exist in the active document: {string.Join(", ", missing)}.");
        }

        uiDoc.Selection.SetElementIds(ids);
        string? warning = null;
        try
        {
            uiDoc.ShowElements(ids);
            uiDoc.RefreshActiveView();
        }
        catch (Exception ex)
        {
            warning = $"Elements were selected, but Revit could not frame all of them: {ex.Message}";
        }

        return new { selected_count = ids.Count, element_ids = ids.Select(id => id.Value).ToArray(), warning };
    }

    private static object CurrentSelection(UIDocument uiDoc)
    {
        long[] ids = uiDoc.Selection.GetElementIds().Select(id => id.Value).ToArray();
        return new { count = ids.Length, element_ids = ids };
    }

    private static object SummarizeElement(Document doc, Element element)
    {
        Dictionary<string, object?> keyParameters = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in new[] { "Mark", "Type Mark", "Comments", "Level" })
        {
            Parameter? parameter = FindParameter(element, name);
            if (parameter is not null && parameter.HasValue)
            {
                keyParameters[name] = ParameterReadableValue(doc, parameter);
            }
        }

        return new
        {
            id = element.Id.Value,
            unique_id = element.UniqueId,
            name = SafeElementName(element),
            category = element.Category?.Name,
            level = GetElementLevelName(doc, element),
            key_parameters = keyParameters
        };
    }

    private static object SerializeParameter(Document doc, Parameter parameter)
    {
        Dictionary<string, object?> data = new()
        {
            ["name"] = parameter.Definition?.Name,
            ["storage_type"] = parameter.StorageType.ToString(),
            ["is_read_only"] = parameter.IsReadOnly,
            ["has_value"] = parameter.HasValue
        };

        if (!parameter.HasValue) return data;

        switch (parameter.StorageType)
        {
            case StorageType.String:
                data["value"] = parameter.AsString();
                break;
            case StorageType.Integer:
                data["value"] = parameter.AsInteger();
                data["display_value"] = SafeAsValueString(parameter);
                break;
            case StorageType.Double:
                data["raw_internal_feet"] = parameter.AsDouble();
                data["display_value"] = SafeAsValueString(parameter);
                ForgeTypeId? dataType = TryGetDataType(parameter);
                bool measurable = dataType is not null && UnitUtils.IsMeasurableSpec(dataType);
                data["spec_type_id"] = dataType?.TypeId;
                data["unit_type_unknown"] = !measurable;
                break;
            case StorageType.ElementId:
                ElementId id = parameter.AsElementId();
                data["value"] = id.Value;
                data["referenced_unique_id"] = doc.GetElement(id)?.UniqueId;
                data["display_value"] = SafeAsValueString(parameter);
                break;
            default:
                data["value"] = null;
                break;
        }

        return data;
    }

    private static object? ParameterReadableValue(Document doc, Parameter parameter)
    {
        return parameter.StorageType switch
        {
            StorageType.String => parameter.AsString(),
            StorageType.Integer => SafeAsValueString(parameter) is string intText ? intText : parameter.AsInteger(),
            StorageType.Double => SafeAsValueString(parameter) is string doubleText ? doubleText : parameter.AsDouble(),
            StorageType.ElementId => doc.GetElement(parameter.AsElementId())?.Name is string elementName
                ? elementName
                : parameter.AsElementId().Value,
            _ => null
        };
    }

    private static bool SetParameterValue(Document doc, Parameter parameter, JsonElement value)
    {
        return parameter.StorageType switch
        {
            StorageType.String => parameter.Set(JsonValueAsString(value)),
            StorageType.Integer => parameter.Set(JsonValueAsInteger(value)),
            StorageType.Double => SetDoubleParameter(parameter, value),
            StorageType.ElementId => parameter.Set(ResolveElementId(doc, value)),
            _ => throw new InvalidOperationException("This parameter has no writable storage type.")
        };
    }

    private static bool SetDoubleParameter(Parameter parameter, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double raw))
        {
            return parameter.Set(raw);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("A double parameter requires a numeric internal-feet value or a formatted unit string.");
        }

        string text = value.GetString() ?? string.Empty;
        if (parameter.SetValueString(text)) return true;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out raw))
        {
            return parameter.Set(raw);
        }

        throw new ArgumentException($"'{text}' is not valid for parameter '{parameter.Definition.Name}'.");
    }

    private static int JsonValueAsInteger(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True) return 1;
        if (value.ValueKind == JsonValueKind.False) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        throw new ArgumentException("An integer parameter requires an integer or boolean value.");
    }

    private static string JsonValueAsString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => throw new ArgumentException("A string parameter requires a string, number, or boolean value.")
        };
    }

    private static ElementId ResolveElementId(Document doc, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long idValue))
        {
            return new ElementId(idValue);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString() ?? string.Empty;
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out idValue))
            {
                return new ElementId(idValue);
            }

            Element? referenced = doc.GetElement(text);
            if (referenced is not null) return referenced.Id;
        }

        throw new ArgumentException("An ElementId parameter requires an integer ID or element unique_id.");
    }

    private static void ValidateMutable(Document doc, Element element)
    {
        // Revit can return different managed wrappers for the same native document.
        // Resolve identity in the active document instead of comparing CLR references.
        Document owner = element.Document;
        Element? activeElement = doc.GetElement(element.UniqueId);
        bool sameDocument = !doc.IsLinked && !owner.IsLinked
            && string.Equals(owner.PathName, doc.PathName, StringComparison.OrdinalIgnoreCase)
            && activeElement is not null && activeElement.Id == element.Id;
        if (!sameDocument)
        {
            throw new InvalidOperationException($"Element {element.Id.Value} belongs to a linked model and cannot be modified.");
        }

        if (doc.IsWorkshared
            && WorksharingUtils.GetCheckoutStatus(doc, element.Id) == CheckoutStatus.OwnedByOtherUser)
        {
            throw new InvalidOperationException($"Element {element.Id.Value} is owned by another user.");
        }
    }

    private static Parameter? FindParameter(Element element, string name)
    {
        return element.GetParameters(name).FirstOrDefault()
            ?? element.Parameters.Cast<Parameter>().FirstOrDefault(parameter => string.Equals(
                parameter.Definition?.Name,
                name,
                StringComparison.CurrentCultureIgnoreCase));
    }

    private static string? GetElementLevelName(Document doc, Element element)
    {
        if (element.LevelId != ElementId.InvalidElementId && doc.GetElement(element.LevelId) is Level level)
        {
            return level.Name;
        }

        foreach (BuiltInParameter builtIn in new[]
                 {
                     BuiltInParameter.FAMILY_LEVEL_PARAM,
                     BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                     BuiltInParameter.SCHEDULE_LEVEL_PARAM
                 })
        {
            Parameter? parameter = element.get_Parameter(builtIn);
            if (parameter?.StorageType == StorageType.ElementId
                && doc.GetElement(parameter.AsElementId()) is Level parameterLevel)
            {
                return parameterLevel.Name;
            }
        }

        return null;
    }

    private static string SafeElementName(Element element)
    {
        try { return element.Name ?? string.Empty; }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { return string.Empty; }
    }

    private static ForgeTypeId? TryGetDataType(Parameter parameter)
    {
        try { return parameter.Definition?.GetDataType(); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { return null; }
    }

    private static string? SafeAsValueString(Parameter parameter)
    {
        try { return parameter.AsValueString(); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { return null; }
    }

    private static string FormatLength(Document doc, double internalFeet)
    {
        try { return UnitFormatUtils.Format(doc.GetUnits(), SpecTypeId.Length, internalFeet, false); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            return internalFeet.ToString("G17", CultureInfo.InvariantCulture) + " ft";
        }
    }

    private static JsonDocument ParseArguments(string json)
    {
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }

    private static JsonElement RequireProperty(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out JsonElement value))
        {
            throw new ArgumentException($"Missing required argument '{name}'.");
        }
        return value;
    }

    private static string RequireString(JsonElement args, string name)
    {
        string? value = OptionalString(args, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"'{name}' must be a non-empty string.")
            : value;
    }

    private static string? OptionalString(JsonElement args, string name)
    {
        return args.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? OptionalInt(JsonElement args, string name)
    {
        return args.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int number)
            ? number
            : null;
    }

    private static ElementId ReadElementId(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long id)) return new ElementId(id);
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
        {
            return new ElementId(id);
        }
        throw new ArgumentException("Every element_id must be a 64-bit integer.");
    }
}
