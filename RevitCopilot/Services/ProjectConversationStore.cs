using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LECG.RevitCopilot.Models;

namespace LECG.RevitCopilot.Services;

internal sealed record ProjectConversation(string ProjectKey, string AccountKey, string ConversationId,
    string? ThreadId, string FileName, string? Model, string? Effort, List<ChatEntry> Messages);

internal sealed class ProjectConversationStore(string root)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string Folder(string account, string project) => Path.Combine(root, Hash(account), Hash(project));

    internal IDisposable Acquire(string account, string project)
    {
        string folder = Folder(account, project);
        Directory.CreateDirectory(folder);
        try { return new FileStream(Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("This project's conversation is already open in another Revit window. Close that connection before continuing here.", ex); }
    }

    internal ProjectConversation? Load(string account, string project)
    {
        string path = Path.Combine(Folder(account, project), "current.json");
        if (!File.Exists(path)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<ProjectConversation>(File.ReadAllText(path), Options);
            if (state is null || state.AccountKey != account || state.ProjectKey != project ||
                string.IsNullOrWhiteSpace(state.ConversationId) || state.Messages is null)
                throw new InvalidDataException("The saved conversation identity is invalid.");
            return state;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidDataException("The saved conversation could not be read. Its files have been preserved; restore current.json.previous before retrying.", ex);
        }
    }

    internal void Save(ProjectConversation state)
    {
        string folder = Folder(state.AccountKey, state.ProjectKey);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "current.json");
        string temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal void Archive(ProjectConversation state)
    {
        Save(state);
        string folder = Folder(state.AccountKey, state.ProjectKey);
        // New conversation never deletes an earlier conversation or its Codex thread.
        File.Copy(Path.Combine(folder, "current.json"), Path.Combine(folder, "history-" + Hash(state.ConversationId) + ".json"), overwrite: true);
    }
}
