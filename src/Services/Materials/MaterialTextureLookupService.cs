using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialTextureLookupService : IMaterialTextureLookupService
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".exr", ".hdr" };

        private static readonly string[] NormalMapKeywords = { "normal", "normalgl", "normal_gl", "normaldx", "normal_dx", "nrm", "nor" };

        private static readonly Dictionary<string, string[]> ChannelKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Diffuse"] = new[] { "basecolor", "base_color", "albedo", "diffuse", "color", "_col" },
            ["Normal"] = NormalMapKeywords,
            ["Roughness"] = new[] { "specularroughness", "specular_roughness", "roughness", "rough", "rgh" },
        };

        /// <summary>
        /// True when the file name looks like a normal map (same keyword/boundary rule ScanFolder uses
        /// to pick the Normal channel). Used by render-match to flip only genuine normal maps to
        /// DataType=Normal and leave real height/bump maps untouched.
        /// </summary>
        public static bool IsNormalMapFileName(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string name = Path.GetFileNameWithoutExtension(path);
            return NormalMapKeywords.Any(kw => NameMatchesKeyword(name, kw));
        }

        private static readonly string[] DiffuseNameSuffixes =
        {
            "_BaseColor", "-BaseColor", " BaseColor",
            "_Base_Color", "-Base_Color", " Base_Color",
            "_Albedo", "-Albedo", " Albedo",
            "_Diffuse", "-Diffuse", " Diffuse",
            "_Color", "-Color", " Color",
            "_Col", "-Col", " Col"
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

        public IReadOnlyList<PbrMaterialFolderCandidate> ScanImmediateMaterialFolders(string rootFolder)
        {
            var candidates = new List<PbrMaterialFolderCandidate>();

            if (!Directory.Exists(rootFolder))
            {
                return candidates;
            }

            foreach (string folder in Directory.GetDirectories(rootFolder).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Dictionary<string, string?> detected = ScanFolder(folder);
                detected.TryGetValue("Diffuse", out string? diffuse);
                detected.TryGetValue("Roughness", out string? roughness);
                detected.TryGetValue("Normal", out string? normal);

                int detectedCount = detected.Values.Count(path => !string.IsNullOrWhiteSpace(path));
                string materialName = diffuse == null ? string.Empty : DeriveMaterialName(diffuse);
                string? skipReason = diffuse == null ? "No diffuse/albedo texture found." : null;

                candidates.Add(new PbrMaterialFolderCandidate(
                    folder,
                    materialName,
                    diffuse,
                    roughness,
                    normal,
                    detectedCount,
                    skipReason));
            }

            return candidates;
        }

        public string DeriveMaterialName(string diffusePath)
        {
            string name = Path.GetFileNameWithoutExtension(diffusePath);
            foreach (string suffix in DiffuseNameSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(0, name.Length - suffix.Length).Trim();
                }
            }

            return name.Trim();
        }

        private static bool NameMatchesKeyword(string fileNameNoExt, string keyword)
        {
            int idx = fileNameNoExt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            bool atStart = idx == 0 || fileNameNoExt[idx - 1] is '_' or '-' or ' ';
            int afterIdx = idx + keyword.Length;
            bool atEnd = afterIdx >= fileNameNoExt.Length || fileNameNoExt[afterIdx] is '_' or '-' or ' ' or '.';
            return atStart || atEnd;
        }

        private static string? FindBestMatch(string[] files, string[] keywords)
        {
            // Try exact keyword boundaries first (e.g., "_basecolor." or "_normal_")
            // then fall back to substring contains
            foreach (string keyword in keywords)
            {
                foreach (string file in files)
                {
                    // Keyword as a delimited segment: preceded by _, -, or start; followed by _, -, . or end.
                    if (NameMatchesKeyword(Path.GetFileNameWithoutExtension(file), keyword))
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
