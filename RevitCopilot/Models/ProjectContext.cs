namespace LECG.RevitCopilot.Models;

// Immutable snapshots only: no Revit API objects cross into the chat worker.
internal sealed record ProjectContext(string Key, string RuntimeKey, string FileName, string Location, bool Persistent);
