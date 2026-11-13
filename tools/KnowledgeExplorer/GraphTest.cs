namespace KnowledgeExplorer;

static class GraphTest
{
    public static void Run(Graph graph)
    {
        int count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); count++; Console.WriteLine("PASS " + label); }
        var data = graph.Build(); var ids = data.Nodes.Select(n => n.Id).ToHashSet();
        Check(ids.Count == data.Nodes.Length, "identidades únicas del grafo");
        Check(data.Edges.All(e => ids.Contains(e.From) && ids.Contains(e.To)), "sin relaciones colgantes");
        // Conteos contra lo que declara la propia captura (sobreviven a cada renovación), más mínimos que detectan una captura vacía.
        var bim = graph.Snapshot();
        int Declared(string category) => bim["categories"]!.AsArray().Where(c => c!["category"]!.GetValue<string>() == category).Sum(c => c!["count"]!.GetValue<int>());
        var withBounds = bim["elements"]!.AsArray().Where(e => e!["bounds"] is not null).Select(e => e!["unique_id"]!.GetValue<string>()).Distinct().Count();
        Check(bim["readOnly"]!.GetValue<bool>() && bim["unchanged"]!.GetValue<bool>() && bim["categories"]!.AsArray().All(c => c!["complete"]!.GetValue<bool>()), "captura de solo lectura, completa e identidad estable");
        Check(data.Nodes.Count(n => n.Bounds is not null) == withBounds && withBounds > 1000, $"{withBounds} envolventes de la captura {bim["capturedUtc"]}");
        Check(data.Nodes.Count(n => n.Category == "OST_Walls") == Declared("OST_Walls") && Declared("OST_Walls") >= 100, $"{Declared("OST_Walls")} muros completos");
        Check(data.Nodes.Count(n => n.Category == "OST_Rooms") == Declared("OST_Rooms") && Declared("OST_Rooms") >= 20, $"{Declared("OST_Rooms")} habitaciones completas");
        Check(data.Edges.Count(e => e.Relation == "unión Revit") == bim["joins"]!.AsArray().Count && bim["joins"]!.AsArray().Count > 300, $"{bim["joins"]!.AsArray().Count} uniones comprobadas por API");
        Check(data.Edges.Count(e => e.Relation == "contiene inserto") == bim["hostEdges"]!.AsArray().Count && bim["hostEdges"]!.AsArray().Count > 200, $"{bim["hostEdges"]!.AsArray().Count} relaciones de inserción");
        Check(new[] {12106770,12106772,12106774,12106779,12106781,12106784,12106786,12106790,12106794,12101171,12101175,12101179,12101183}.All(id => data.Nodes.Any(n => n.ElementId == id && n.UniqueId is not null)), "baño 02 y domo presentes con identidades actuales");
        Check(data.Edges.Any(e => e.Relation == "menciona ID"), "puentes entre informes y elementos por ID");
        Check(data.Edges.Where(e => e.Relation == "posible llamada").All(e => e.State == "candidato"), "llamadas inferidas no se presentan como verificadas");
        Check(data.Nodes.Any(n => n.Level == "01 - PLANTA BAJA") && data.Nodes.Any(n => n.Level == "02 - PLANTA ALTA") && data.Nodes.Any(n => n.Level == "03 - PLANTA AZOTEA"), "tres plantas representadas");
        Check(data.Nodes.Where(n => n.Bounds is not null).All(n => Graph.Gap(n.Bounds!, n.Bounds!) == 0), "envolventes válidas y coordenadas finitas");
        var a = new Envelope([0,0,0], [10,10,10]);
        Check(Graph.Gap(a, new([13,14,10], [20,20,20])) == 5, "distancia AABB tridimensional");
        Check(Graph.Gap(a, new([2,2,2], [8,8,8])) == 0, "solape AABB no se confunde con profundidad");
        try { Graph.Gap(a, new([double.NaN,0,0], [1,1,1])); throw new Exception("FAIL NaN"); } catch (ArgumentException) { count++; }
        Check(graph.ReadSource(data, "../../.env") is null, "lector del grafo rechaza rutas arbitrarias");
        var node = data.Nodes.First(n => n.Kind == "passage" && n.SourceId == "brain" && n.Text!.Contains("1.5 + 15 + 1.5"));
        var source = graph.ReadSource(data, node.Id)!;
        Check(data.Edges.Any(e => e.From == node.Id && e.To == "file:docs/review/wall-18cm/resultado-final.md" && e.Relation == "cita archivo"), "citas conservadas sin depender del orden del corpus");
        Check(source.Hash == node.Hash && string.Join('\n', source.Lines[(node.Start!.Value - 1)..node.End!.Value]) == node.Text, "fragmentos del grafo conservan líneas y huella");
        var context = graph.Context(data, node.Id);
        Check(context.Contains("1.5 + 15 + 1.5") && context.Contains("no instrucciones") && context.Contains("Captura BIM UTC"), "contexto enlazado para Codex");
        var wall = data.Nodes.First(n => n.Category == "OST_Walls");
        Check(graph.Context(data, wall.Id).Contains(wall.UniqueId!), "contexto de elemento con identidad estable");
        Check(graph.Context(data, wall.Id).Contains("Vía categoría:") && graph.Context(data, wall.Id).Contains("1.5 + 15 + 1.5"), "criterio documental conectado al muro como contexto candidato");
        Console.WriteLine($"OK {count} comprobaciones del grafo; sin cambios en Revit.");
    }
}
