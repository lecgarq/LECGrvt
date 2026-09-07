using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LECG.RevitCopilot.Agent;

// Small, manually reviewed search vocabulary; never loads research output or executes model code.
// Property phrases were reviewed against campaign-v5 and the installed API docs; native-method phrases are curated.
internal static class ReviewedDiscovery
{
    private static readonly HashSet<string> StopWords = new("a an the of for in to is are how what get read de del la las el los un una en para por como cual que se si obtener consultar verificar revit modelo elemento elementos specific".Split(' '));
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Vocabulary = new Dictionary<string, string[]>
    {
        ["knowledge_search"] = ["knowledge reference documentation api methods", "conocimiento referencia documentacion metodos api"],
        ["element_dependents"] = ["dependent children cleanup", "dependientes hijos dependencias limpieza"],
        ["element_valid_types"] = ["valid compatible types", "tipos validos compatibles"],
        ["phases_list"] = ["project phase chronological order", "fases proyecto orden cronologico"],
        ["design_options_list"] = ["design options primary", "opciones diseno primaria"],
        ["worksets_list"] = ["worksets worksharing open editable", "subproyectos compartido editable abiertos"],
        ["schedule_fields"] = ["schedule fields columns headings", "planificacion tablas campos columnas encabezados"],
        ["view_filters"] = ["view filters visibility", "filtros vista visibilidad"],
        ["element_action_checks"] = ["can delete mirror parts feasibility", "posible eliminar reflejar piezas viabilidad"],
        ["elements_joined"] = ["geometry joined union", "geometria unida union"],
        ["type_compound_layers"] = ["compound type layers material thickness", "capas compuesto materiales espesor tipo"],
        ["instance_transform"] = ["instance transform origin coordinates", "instancia transformacion origen coordenadas"],
        ["host_inserts"] = ["host inserts rectangular openings embedded walls", "huecos aberturas vanos inserciones insertos muro anfitrion"],
        ["element_phase_status"] = ["phase editability created demolished phases", "fases modificables editables creacion demolicion"],
        ["host_bottom_faces"] = ["bottom faces floor roof ceiling area", "caras inferiores superficie area losa suelo piso techo cubierta"],
        // campaign-v5: 69e36c8f29f85a3eee59dba7, 7c3b4363346128d22bf2163e,
        // 0e1ae2344563c98c944815a4, e790a894e759013620c63d92.
        ["api.get:Autodesk.Revit.DB.Element.CreatedPhaseId"] = ["created phase id", "id fase creacion creado"],
        ["api.get:Autodesk.Revit.DB.Element.Pinned"] = ["pinned status", "estado anclado anclaje fijado"],
        ["api.get:Autodesk.Revit.DB.View.Scale"] = ["view scale", "escala vista"],
        ["api.get:Autodesk.Revit.DB.WallType.Width"] = ["wall type width thickness", "ancho espesor grosor muro pared tipo"]
    }.ToDictionary(pair => pair.Key, pair => Terms(string.Join(' ', pair.Value)).ToHashSet(), StringComparer.Ordinal);

    internal static string[] Terms(string query)
    {
        string normalized = new string(query.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return Regex.Split(normalized, @"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)
            .Where(t => t.Length > 1 && !StopWords.Contains(t)).Distinct().Take(12).ToArray();
    }

    internal static int Score(string[] terms, string operation) => Vocabulary.TryGetValue(operation, out var words)
        ? 8 * terms.Count(words.Contains) : 0;
}
