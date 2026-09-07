using System.IO;
using Autodesk.Revit.DB;
using LECG.RevitCopilot.Models;

namespace LECG.RevitCopilot.Revit;

internal static class ProjectContextReader
{
    // Revit returns multiple managed wrappers for one native document. Its Equals
    // compares document identity; CLR reference equality (including CWT) does not.
    // Read is called exclusively from Revit API contexts, never from worker threads.
    private static readonly List<(Document Document, string Runtime)> Identities = [];

    internal static ProjectContext Read(Document doc)
    {
        Identities.RemoveAll(entry => !entry.Document.IsValidObject);
        string? runtime = Identities.FirstOrDefault(entry => entry.Document.Equals(doc)).Runtime;
        if (runtime is null) { runtime = Guid.NewGuid().ToString("N"); Identities.Add((doc, runtime)); }
        string name = doc.Title;
        string extension = doc.IsFamilyDocument ? ".rfa" : ".rvt";
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) name += extension;
        if (doc.IsDetached) return new("detached:" + runtime, runtime, name, "Detached model · session only", false);
        if (doc.IsModelInCloud)
        {
            ModelPath cloud = doc.GetCloudModelPath();
            string key = $"cloud:{cloud.GetProjectGUID():D}:{cloud.GetModelGUID():D}";
            return new(key, runtime, name, doc.PathName, true);
        }
        string path = doc.PathName;
        if (doc.IsWorkshared)
        {
            ModelPath? central = doc.GetWorksharingCentralModelPath();
            if (central is not null)
            {
                string centralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(central);
                if (!string.IsNullOrWhiteSpace(centralPath)) path = centralPath;
            }
        }
        if (string.IsNullOrWhiteSpace(path)) return new("unsaved:" + runtime, runtime, name, "Save this file to resume after restarting Revit", false);
        string normalized = path.StartsWith("RSN://", StringComparison.OrdinalIgnoreCase)
            ? path.ToUpperInvariant() : Path.GetFullPath(path).TrimEnd('\\').ToUpperInvariant();
        return new("file:" + normalized, runtime, name, path, true);
    }
}
