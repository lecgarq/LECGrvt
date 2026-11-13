using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KnowledgeExplorer;

// Memoria del agente y conversaciones de Claude Code y Codex de este repositorio, como fuentes de solo lectura.
static class Sessions
{
    const string State = "Conversación con el agente; lo dicho no prueba que se aplicó ni que siga vigente";
    static readonly EnumerationOptions Flat = new() { AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System };
    static readonly EnumerationOptions Deep = new() { RecurseSubdirectories = true, AttributesToSkip = Flat.AttributesToSkip };
    static readonly Regex Injected = new(@"<(system-reminder|environment_context|INSTRUCTIONS|app-context|command-[a-z-]+|local-command-[a-z-]+)>[\s\S]*?</\1>", RegexOptions.Compiled);

    public static string Slug(string root) => Regex.Replace(Path.GetFullPath(root).TrimEnd('\\', '/'), @"[:\\/ ]", "-");
    // Fuera de AppData: las apps empaquetadas (como la de Claude) virtualizan AppData y otros procesos no verían estos archivos.
    public static string Cache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lecg", "KnowledgeExplorer", "sesiones");
    public static string Memory(string home, string root) => Path.Combine(home, ".claude", "projects", Slug(root), "memory");

    public static IEnumerable<SourceSpec> Specs(string root, string home)
    {
        var memory = Memory(home, root);
        if (Directory.Exists(memory))
            foreach (var f in Directory.EnumerateFiles(memory, "*.md", Flat).Order())
                yield return new("memoria-" + Retrieval.Id(f), "memoria/" + Path.GetFileName(f), "both", "Memoria del agente; nota breve, comprobar contra el repositorio", f);
        var claude = Path.Combine(home, ".claude", "projects", Slug(root));
        var codex = Path.Combine(home, ".codex", "sessions");
        var files = (Directory.Exists(claude) ? Directory.EnumerateFiles(claude, "*.jsonl", Flat).Select(f => (kind: "claude", f)) : [])
            .Concat(Directory.Exists(codex) ? Directory.EnumerateFiles(codex, "*.jsonl", Deep).Select(f => (kind: "codex", f)) : []);
        Directory.CreateDirectory(Cache);
        foreach (var (kind, f) in files.OrderBy(x => x.f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var cache = Path.Combine(Cache, $"{kind}-{name}.md");
            var stamp = File.GetLastWriteTimeUtc(f);
            // ponytail: se reextrae la conversación entera cuando cambia; extracción incremental si una activa tarda demasiado.
            if (!File.Exists(cache) || File.GetLastWriteTimeUtc(cache) != stamp)
            {
                File.WriteAllText(cache, Extract(kind, f, root), new UTF8Encoding(false));
                File.SetLastWriteTimeUtc(cache, stamp);
            }
            if (new FileInfo(cache).Length > 0)
                yield return new("sesion-" + Retrieval.Id(cache), $"sesiones/{kind}/{name}", "sessions", State, cache);
        }
    }

    // ponytail: solo el texto de usuario y asistente; sin herramientas, razonamiento ni contexto inyectado. Añadirlos si hay que citar salidas de comandos.
    internal static string Extract(string kind, string file, string root)
    {
        var body = new StringBuilder(); string? title = null, last = null; DateTime? first = null;
        using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        for (string? line; (line = reader.ReadLine()) is not null;)
        {
            if (!line.Contains("\"role\":\"user\"") && !line.Contains("\"role\":\"assistant\"") && !line.Contains("\"session_meta\"")) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var j = doc.RootElement;
                var type = j.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type == "session_meta")
                {
                    var cwd = j.GetProperty("payload").TryGetProperty("cwd", out var c) ? c.GetString() ?? "" : "";
                    if (!Path.GetFullPath(cwd + "\\").StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return "";
                    continue;
                }
                JsonElement message;
                if (kind == "claude")
                {
                    if (type is not ("user" or "assistant") || Flag(j, "isSidechain") || Flag(j, "isCompactSummary") || Flag(j, "isMeta")) continue;
                    message = j.GetProperty("message");
                }
                else
                {
                    if (type != "response_item" || !j.TryGetProperty("payload", out message) || message.GetProperty("type").GetString() != "message") continue;
                }
                var role = message.GetProperty("role").GetString();
                if (role is not ("user" or "assistant") || !message.TryGetProperty("content", out var content)) continue;
                var text = Clean(content.ValueKind == JsonValueKind.String ? content.GetString()! : string.Join("\n",
                    content.EnumerateArray().Where(b => b.TryGetProperty("text", out _) && b.GetProperty("type").GetString() is "text" or "input_text" or "output_text")
                        .Select(b => b.GetProperty("text").GetString())));
                if (text.Length == 0) continue;
                var when = j.TryGetProperty("timestamp", out var ts) && ts.TryGetDateTime(out var d) ? d.ToLocalTime() : (DateTime?)null;
                first ??= when;
                if (role == "user") title ??= Retrieval.Cut(text.Split('\n')[0], 90);
                var heading = $"## {when:yyyy-MM-dd HH:mm} · {(role == "user" ? "usuario" : "asistente")}";
                if (heading != last) body.Append('\n').Append(heading).Append("\n\n");
                last = heading;
                body.Append(text).Append('\n');
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return body.Length == 0 ? "" : $"# Conversación {kind} · {first:yyyy-MM-dd} · {title}\n{body}";
    }

    static bool Flag(JsonElement j, string name) => j.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    static string Clean(string text)
    {
        text = Injected.Replace(text, "").Replace("\r\n", "\n").Trim();
        if (text.StartsWith('<') || text.StartsWith("# AGENTS.md instructions")) return "";
        if (text.Length > 4000) text = Retrieval.Cut(text, 4000) + " […]";
        // Los encabezados dentro de un mensaje romperían la fecha del fragmento.
        return string.Join('\n', text.Split('\n').Select(l => l.StartsWith('#') ? "\\" + l : l));
    }
}
