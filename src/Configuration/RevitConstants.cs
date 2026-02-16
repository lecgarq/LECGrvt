using System.Collections.Generic;

namespace LECG.Configuration
{
    /// <summary>
    /// Centralized constants for Revit built-in names and categories.
    /// </summary>
    public static class RevitConstants
    {
        public static readonly HashSet<string> BuiltInLineStyles = new HashSet<string>
        {
            "Thin Lines", "Medium Lines", "Wide Lines", "Hidden Lines",
            "Overhead", "Demolished", "Beyond", "Centerline",
            "Axis of Rotation", "Lines", "Insulation Batting Lines",
            "Path of Travel Lines", "<Sketch>", "<Area Boundary>" 
        };

        public static readonly HashSet<string> BuiltInMaterials = new HashSet<string>
        {
            "Default", "Default Wall", "Default Roof",
            "Default Floor", "Default Ceiling", "Air",
            "Glass", "Earth"
        };

        public static readonly HashSet<string> BuiltInPatterns = new HashSet<string>
        {
            "Solid fill", "No Pattern"
        };

        public static bool IsBuiltInLineStyle(string name)
        {
            return name.StartsWith("<") || name.EndsWith(">") || BuiltInLineStyles.Contains(name);
        }

        public static bool IsBuiltInMaterial(string name)
        {
            return BuiltInMaterials.Contains(name);
        }

        public static bool IsBuiltInFillPattern(string name)
        {
            return (name.StartsWith("<") && name.EndsWith(">")) || BuiltInPatterns.Contains(name);
        }
    }
}
