using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KnowledgeExplorer;

// Búsqueda por significado con bge-m3 local (embed.py). Si Python o el modelo faltan, la búsqueda sigue solo por palabras.
public sealed class Semantic(string script) : IDisposable
{
    const int Dim = 1024, Record = 16 + Dim * 2;
    static string Store => Path.Combine(Path.GetDirectoryName(Sessions.Cache)!, "vectores-bge-m3.bin");
    readonly Dictionary<string, Half[]> vectors = Load();
    readonly object gate = new(), work = new();
    readonly Queue<string> stderr = new();
    Process? worker;
    public string? Error { get; private set; }
    public int Count { get { lock (gate) return vectors.Count; } }

    static Half[] Halves(byte[] bytes, int at) => MemoryMarshal.Cast<byte, Half>(bytes.AsSpan(at, Dim * 2)).ToArray();
    static string Key(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))[..16]);

    // Almacén de solo anexar: 16 bytes de huella del texto + 1024 float16. Un registro incompleto al final se ignora.
    static Dictionary<string, Half[]> Load()
    {
        var map = new Dictionary<string, Half[]>();
        if (!File.Exists(Store)) return map;
        var bytes = File.ReadAllBytes(Store);
        for (int at = 0; at + Record <= bytes.Length; at += Record)
        {
            map[Convert.ToHexString(bytes, at, 16)] = Halves(bytes, at + 16);
        }
        return map;
    }

    Half[][]? Embed(IReadOnlyList<string> texts)
    {
        lock (work)
        {
            if (Error is not null) return null;
            try
            {
                worker ??= Start();
                worker.StandardInput.WriteLine(JsonSerializer.Serialize(texts));
                var bytes = Convert.FromBase64String(worker.StandardOutput.ReadLine() ?? throw new IOException("embed.py terminó."));
                if (bytes.Length != texts.Count * Dim * 2) throw new IOException("embed.py devolvió un tamaño inesperado.");
                return [.. Enumerable.Range(0, texts.Count).Select(i => Halves(bytes, i * Dim * 2))];
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or FormatException)
            {
                lock (stderr) Error = $"Búsqueda semántica no disponible: {ex.Message} {string.Join(" ", stderr)}".Trim();
                try { worker?.Kill(); } catch (InvalidOperationException) { }
                worker = null;
                return null;
            }
        }
    }

    // Python con torch CUDA en un entorno propio si existe; si no, `py -3.12` (torch solo CPU) del sistema.
    static string GpuPython => Path.Combine(Path.GetDirectoryName(Sessions.Cache)!, "venv-gpu", "Scripts", "python.exe");
    public string Device { get; private set; } = "sin iniciar";
    Process Start()
    {
        var gpu = File.Exists(GpuPython);
        string[] arguments = gpu ? [script] : ["-3.12", script];
        var info = new ProcessStartInfo(gpu ? GpuPython : "py", arguments)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.ASCII
        };
        var p = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar Python.");
        p.ErrorDataReceived += (_, e) => { if (e.Data is null) return; lock (stderr) { stderr.Enqueue(e.Data); if (stderr.Count > 5) stderr.Dequeue(); } };
        p.BeginErrorReadLine();
        var ready = p.StandardOutput.ReadLine();
        if (ready?.StartsWith("ready ") != true) throw new IOException("embed.py no cargó bge-m3.");
        Device = ready[6..] == "cuda" ? "GPU" : "CPU";
        return p;
    }

    // Calcula los vectores que faltan, por lotes, y los anexa al almacén. Devuelve cuántos añadió.
    public int Index(IEnumerable<string> texts, int limit = int.MaxValue, Action<int, int>? progress = null)
    {
        string[] missing;
        lock (gate) missing = [.. texts.Distinct().Where(t => !vectors.ContainsKey(Key(t))).Take(limit)];
        int done = 0;
        foreach (var batch in missing.Chunk(32))
        {
            var v = Embed(batch);
            if (v is null) break;
            lock (gate)
            {
                // Un solo Write por lote y acceso exclusivo: el servidor y la CLI pueden anexar a la vez.
                // Si otro proceso tiene el archivo, el lote queda en memoria y se vuelve a calcular en el próximo arranque.
                var records = new List<byte>();
                for (int i = 0; i < batch.Length; i++)
                {
                    var key = Key(batch[i]);
                    if (!vectors.TryAdd(key, v[i])) continue;
                    records.AddRange(Convert.FromHexString(key));
                    records.AddRange(MemoryMarshal.AsBytes(v[i].AsSpan()).ToArray());
                }
                try
                {
                    using var file = new FileStream(Store, FileMode.Append, FileAccess.Write, FileShare.Read);
                    file.Write(records.ToArray());
                }
                catch (IOException) { }
            }
            done += batch.Length;
            progress?.Invoke(done, missing.Length);
        }
        return done;
    }

    public float[]? Query(string text) => Embed([text])?[0].Select(h => (float)h).ToArray();

    // Producto punto con vectores normalizados = similitud coseno; null si el texto aún no tiene vector.
    public double? Similarity(float[] query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        Half[]? v;
        lock (gate) v = vectors.GetValueOrDefault(Key(text));
        if (v is null) return null;
        double sum = 0;
        for (int i = 0; i < Dim; i++) sum += query[i] * (float)v[i];
        return sum;
    }

    public void Dispose() { try { worker?.Kill(); } catch (InvalidOperationException) { } worker?.Dispose(); }
}
