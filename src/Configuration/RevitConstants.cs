using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LECG.Configuration
{
    /// <summary>
    /// Centralized constants for Revit built-in names and categories.
    /// </summary>
    public static class RevitConstants
    {
        public const string FamilyTemplatesBaseDirFormat = @"C:\ProgramData\Autodesk\RVT {0}\Family Templates";

        public static string GetFamilyTemplatesBaseDir(string versionNumber) =>
            string.Format(FamilyTemplatesBaseDirFormat, versionNumber);

        /// <summary>
        /// Searches all installed Revit versions for a template at the given relative path
        /// under the Family Templates directory. Prefers the most recent version.
        /// </summary>
        public static string? FindTemplate(string relativePath)
        {
            string programData = @"C:\ProgramData\Autodesk";
            if (!Directory.Exists(programData)) return null;

            var rvtDirs = Directory.GetDirectories(programData, "RVT *")
                .OrderByDescending(d => d);

            foreach (string dir in rvtDirs)
            {
                string fullPath = Path.Combine(dir, "Family Templates", relativePath);
                if (File.Exists(fullPath)) return fullPath;
            }

            return null;
        }

        public static readonly IReadOnlySet<string> BuiltInLineStyles = new HashSet<string>
        {
            "Thin Lines", "Medium Lines", "Wide Lines", "Hidden Lines",
            "Overhead", "Demolished", "Beyond", "Centerline",
            "Axis of Rotation", "Lines", "Insulation Batting Lines",
            "Path of Travel Lines", "<Sketch>", "<Area Boundary>"
        };

        public static readonly IReadOnlySet<string> BuiltInMaterials = new HashSet<string>
        {
            "Default", "Default Wall", "Default Roof",
            "Default Floor", "Default Ceiling", "Air",
            "Glass", "Earth"
        };

        public static readonly IReadOnlySet<string> BuiltInPatterns = new HashSet<string>
        {
            "Solid fill", "No Pattern"
        };

        public static bool IsBuiltInLineStyle(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return (name.StartsWith("<") && name.EndsWith(">")) || BuiltInLineStyles.Contains(name);
        }

        public static bool IsBuiltInMaterial(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return BuiltInMaterials.Contains(name);
        }

        public static bool IsBuiltInFillPattern(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return (name.StartsWith("<") && name.EndsWith(">")) || BuiltInPatterns.Contains(name);
        }
    }
}
