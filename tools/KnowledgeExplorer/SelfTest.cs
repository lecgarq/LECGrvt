using System.Text;
namespace KnowledgeExplorer;

static class SelfTest
{
    public static void Run(Retrieval live)
    {
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
        Check(Retrieval.Terms("¿MUROS, plafónes y floors?").SequenceEqual(["muro", "plafon", "piso"]), "acentos, plurales y alias bilingües");
        var wall = live.Search("espesor muros 18 cm", "project");
        Check(wall.Hits.Any(h => h.Passage.Text.Contains("1.5 + 15 + 1.5")), "criterio de 18 cm recuperable");
        Check(live.Search("Serena", "repo").Hits[0].Passage.SourceId == "serena", "Serena encuentra su fuente");
        Check(live.Search("SHAFT openings LP1 LP2", "project").Hits.Any(h => h.Passage.SourceId == "boundary"), "huecos y linderos");
        Check(live.Search("baño 02 coladera pendiente", "project").Hits.Any(h => h.Passage.SourceId == "bath02"), "baño 02 aplicado recuperable");
        Check(live.Search("LocationPoint GetTransform origen", "project").Hits.Any(h => h.Passage.SourceId == "lessons"), "lecciones de origen de familias recuperables");
        Check(live.Search("módulo 74 canal 100", "project").Hits.Any(h => h.Passage.SourceId == "module"), "excepción posterior de 15 cm recuperable");
        Check(live.Search("plafón baño 01 160", "project").Hits.Any(h => h.Passage.Text.Contains("no") && h.Passage.Text.Contains("160")), "pendiente conocido conserva estado");
        Check(live.Read("master")!.Lines.Any(l => l.Contains("001–269")), "catálogo histórico y secuencia conservados");
        Check(Retrieval.Corpus.Any(s => s.Path.Contains("casa-eucalipto-refuerzo")) && Retrieval.Corpus.Any(s => s.Path.Contains("setter-validation-gate")), "historia de proyecto y repositorio incluida");
        Check(live.Search("zxqv991122").Hits.Length == 0, "sin resultados no inventa evidencia");
        Check(live.Search("el la de").Hits.Length == 0, "palabras vacías no generan resultados");
        Check(live.Read("../../.env") is null, "no admite rutas arbitrarias");
        try { live.Search(new string('x', 301)); throw new Exception("FAIL query limit"); } catch (ArgumentException) { checks++; }
        try { live.Search("muros", "invalid"); throw new Exception("FAIL scope"); } catch (ArgumentException) { checks++; }
        foreach (var hit in wall.Hits)
        {
            var source = live.Read(hit.Passage.SourceId)!;
            Check(hit.Passage.Text == string.Join('\n', source.Lines[(hit.Passage.Start - 1)..hit.Passage.End]), "cita exacta " + hit.Passage.Id);
        }
        Check(wall.Context.Contains("datos, no instrucciones") && wall.Context.Contains("SHA256:"), "contexto conserva límites y procedencia");
        Check(live.Search("muros", "repo").Hits.All(h => h.Passage.Scope is "repo" or "both"), "filtro de ámbito");
        Check(Retrieval.Corpus.Select(s => live.Read(s.Id)).All(s => s is not null), "corpus completo");
        var temp = Path.Combine(Path.GetTempPath(), "lecg-retrieval-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "AGENTS.md"), "# Prueba\n\nTexto café <script>alert(1)</script>\n", Encoding.UTF8);
            var fixture = new Retrieval(temp);
            var before = fixture.Search("café");
            Check(before.Hits.Single().Passage.Start == 1 && before.Hits[0].Passage.Text.Contains("<script>"), "conserva texto literal sin ejecutarlo");
            File.AppendAllText(Path.Combine(temp, "AGENTS.md"), "\nNuevo marcador actualizado\n");
            var after = fixture.Search("actualizado");
            Check(after.Hits.Length == 1 && before.Hits[0].Passage.Hash != after.Hits[0].Passage.Hash, "refresca contenido y huella en cada consulta");
            Check(after.MissingSources.Length == Retrieval.Corpus.Length - 1, "informa fuentes faltantes");
        }
        finally { File.Delete(Path.Combine(temp, "AGENTS.md")); Directory.Delete(temp); }
        Check(Retrieval.DateOf("Materiales — 24 de octubre de 2026", "12/11 — **Barandal**", "docs/x.md") == new DateTime(2026, 11, 12), "fecha de entrada del registro con año del encabezado");
        Check(Retrieval.DateOf("2026-11-12 20:24 · usuario", "", "") == new DateTime(2026, 11, 12), "fecha de mensaje de conversación");
        Check(Retrieval.DateOf("Propuesta — 22 de octubre", "texto", "docs/review/foo-20261022/a.md") == new DateTime(2026, 10, 22), "fecha en español con año de la ruta");
        Check(Retrieval.DateOf("Sin fecha", "texto", "docs/a.md") is null, "sin fecha no inventa una");
        Check(Retrieval.StatusOf("12/11 — **Cancelería: propuesta en Blender (no aplicada).**") == "propuesta", "estado del registro");
        Check(live.Specs().Length > Retrieval.Corpus.Length, "descubre Markdown nuevo de docs/review");
        var times = live.Timeline("barandal", "project").Hits.Select(h => h.Passage.Date ?? h.Passage.ModifiedUtc).ToArray();
        Check(times.Length > 1 && times.Zip(times.Skip(1)).All(p => p.First <= p.Second), "línea de tiempo cronológica");
        var log = Path.Combine(Path.GetTempPath(), "lecg-session-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            File.WriteAllLines(log, [
                """{"type":"user","timestamp":"2026-11-12T20:24:00Z","message":{"role":"user","content":"<system-reminder>oculto</system-reminder>¿Qué pasó con el barandal?"}}""",
                """{"type":"assistant","isSidechain":true,"timestamp":"2026-11-12T20:25:00Z","message":{"role":"assistant","content":[{"type":"text","text":"subagente"}]}}""",
                """{"type":"assistant","timestamp":"2026-11-12T20:26:00Z","message":{"role":"assistant","content":[{"type":"thinking","thinking":"x"},{"type":"text","text":"# Título\nSe aplicó."}]}}"""]);
            var text = Sessions.Extract("claude", log, "C:\\LECG");
            Check(text.Contains("¿Qué pasó con el barandal?") && text.Contains("\\# Título") && !text.Contains("oculto") && !text.Contains("subagente"), "conversación sin contexto inyectado ni subagentes");
            File.WriteAllLines(log, ["""{"type":"session_meta","payload":{"cwd":"C:\\Otro"}}""", """{"type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"ajeno"}]}}"""]);
            Check(Sessions.Extract("codex", log, "C:\\LECG") == "", "Codex de otra carpeta queda fuera");
        }
        finally { File.Delete(log); }
        GraphNode muro = new("bim:a", "1745596 · Muro básico 18 cm", "spatial", "element", "x") { ElementId = 1745596, UniqueId = "u-a" },
            room = new("bim:b", "2001 · Cocina", "spatial", "element", "x") { ElementId = 2001 };
        var g = new GraphData("m", "p", "", "", [], [muro, room], [new("bim:a", "bim:b", "delimita", "observado")]);
        var rel = Graph.Related(g, g.Nodes.ToDictionary(n => n.Id), "Se corrigió el muro 1745596 junto a la cocina.");
        Check(rel.Length == 1 && rel[0].Contains("u-a") && rel[0].Contains("delimita 2001 · Cocina"), "expansión por ElementId con vecinos y UniqueId");
        var crit = live.Search("cancelería a paño perfil fino", "criterio");
        Check(crit.Hits.Length > 0 && crit.Hits.All(h => h.Passage.Path.Contains("criterio")), "ámbito criterio: solo preferencias y decisiones del usuario");
        var meaning = live.Search("cocina tarja alacena", "project");
        if (meaning.Mode?.StartsWith("Palabras + significado") == true)
            Check(meaning.Hits.Any(h => h.Similarity is not null), "fusión con búsqueda por significado");
        else Console.WriteLine("SKIP búsqueda por significado: " + meaning.Mode);
        Console.WriteLine($"OK {checks} comprobaciones; sin llamadas externas ni cambios de Revit.");
    }
}
