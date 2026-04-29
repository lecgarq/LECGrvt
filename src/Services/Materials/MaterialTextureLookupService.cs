using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialTextureLookupService : IMaterialTextureLookupService
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".exr", ".hdr" };

        private static readonly Dictionary<string, string[]> ChannelKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Diffuse"] = new[] { "basecolor", "base_color", "albedo", "diffuse", "color", "_col" },
            ["Normal"] = new[] { "normal", "normalgl", "normal_gl", "normaldx", "normal_dx", "nrm", "nor" },
            ["Roughness"] = new[] { "roughness", "rough", "rgh" },
            ["Metallic"] = new[] { "metallic", "metalness", "metal", "met" },
            ["AO"] = new[] { "ambientocclusion", "ambient_occlusion", "ao", "occlusion" },
            ["Displacement"] = new[] { "displacement", "disp", "height", "bump" },
            ["Opacity"] = new[] { "opacity", "alpha", "transparency", "mask" },
        };

        public string? FindTextureFile(string folder, string partialName)
        {
            if (!Directory.Exists(folder)) return null;

            return Directory.GetFiles(folder)
                .FirstOrDefault(f => Path.GetFileName(f).IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public Dictionary<string, string?> ScanFolder(string folder)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (string channel in ChannelKeywords.Keys)
            {
                result[channel] = null;
            }

            if (!Directory.Exists(folder)) return result;

            string[] files = Directory.GetFiles(folder)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();

            foreach (var kvp in ChannelKeywords)
            {
                string channel = kvp.Key;
                string[] keywords = kvp.Value;

                string? match = FindBestMatch(files, keywords);
                if (match != null)
                {
                    result[channel] = match;
                }
            }

            return result;
        }

        private static string? FindBestMatch(string[] files, string[] keywords)
        {
            // Try exact keyword boundaries first (e.g., "_basecolor." or "_normal_")
            // then fall back to substring contains
            foreach (string keyword in keywords)
            {
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    string kw = keyword.ToLowerInvariant();

                    // Check for keyword as a delimited segment: preceded by _, -, or start; followed by _, -, . or end
                    int idx = name.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;

                    bool atStart = idx == 0 || name[idx - 1] == '_' || name[idx - 1] == '-' || name[idx - 1] == ' ';
                    int afterIdx = idx + kw.Length;
                    bool atEnd = afterIdx >= name.Length || name[afterIdx] == '_' || name[afterIdx] == '-' || name[afterIdx] == ' ' || name[afterIdx] == '.';

                    if (atStart || atEnd)
                    {
                        return file;
                    }
                }
            }

            // Fallback: simple substring match
            foreach (string keyword in keywords)
            {
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return file;
                    }
                }
            }

            return null;
        }
    }
}
