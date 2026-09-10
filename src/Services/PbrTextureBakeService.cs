using System;
using System.Collections.Generic;
using System.IO;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Imaging;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PbrTextureBakeService : IPbrTextureBakeService
    {
        public BakedTextureSet Bake(SubstanceMaterialEntry entry, BakeOptions options, Action<string>? log = null)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(options);

            BakeOutputPaths paths = BakeOutputPaths.For(entry, options.OutputRoot);
            List<(string path, long ticks)> sources = CollectSources(entry);
            const bool normalConventionDeclared = true;
            string sourceNormalConvention = entry.NormalFormat;
            bool aoBakedIntoBaseColor = entry.AoPath is not null;

            BakeSidecar? existing = File.Exists(paths.Sidecar) ? BakeSidecar.FromJson(File.ReadAllText(paths.Sidecar)) : null;
            if (!options.ForceRebake
                && BakeSidecar.IsFresh(existing, options.TargetSize, sources, sourceNormalConvention, normalConventionDeclared, aoBakedIntoBaseColor)
                && OutputsExist(paths, existing!))
            {
                log?.Invoke($"    -> Bake up to date: {paths.Folder}");
                return new BakedTextureSet(
                    paths.BaseColor, paths.NormalGl, paths.Roughness,
                    existing!.F0Written ? paths.F0 : null,
                    existing.OpacityWritten ? paths.Opacity : null,
                    WasSkipped: true);
            }

            Directory.CreateDirectory(paths.Folder);
            int size = options.TargetSize;

            // Base color (+AO, +metal albedo scaling)
            var (baseColor, w, h) = PngIo.LoadBgra32(entry.BaseColorPath, size);
            byte[] baseForF0 = entry.AoPath is not null ? (byte[])baseColor.Clone() : baseColor;
            if (entry.AoPath is not null)
            {
                var (ao, _, _) = PngIo.LoadGray8(entry.AoPath, size);
                PbrBakeMath.MultiplyByGray(baseColor, ao);
            }

            var (metallic, _, _) = PngIo.LoadGray8(entry.MetallicPath, size);
            bool isMetal = PbrBakeMath.IsMetallic(PbrBakeMath.MaxGray(metallic));
            string? f0Path = null;
            if (isMetal)
            {
                byte[] f0 = PbrBakeMath.ComputeF0(baseForF0, metallic);
                PngIo.SaveBgr24FromBgra(paths.F0, f0, w, h);
                PbrBakeMath.ScaleAlbedoByInverseMetallic(baseColor, metallic);
                f0Path = paths.F0;
            }
            PngIo.SaveBgr24FromBgra(paths.BaseColor, baseColor, w, h);

            // Autodesk's surface_normal slot consumes OpenGL tangent-space normals.
            // The scanner requires each manifest to declare its source convention.
            var (normal, nw, nh) = PngIo.LoadBgra32(entry.NormalPath, size);
            if (string.Equals(sourceNormalConvention, "DirectX", StringComparison.Ordinal))
            {
                PbrBakeMath.InvertGreen(normal);
            }
            PngIo.SaveBgr24FromBgra(paths.NormalGl, normal, nw, nh);

            // Roughness: 16 -> 8 bit
            var (rough, rw, rh) = PngIo.LoadGray8(entry.RoughnessPath, size);
            PngIo.SaveGray8(paths.Roughness, rough, rw, rh);

            // Opacity
            string? opacityPath = null;
            if (entry.OpacityPath is not null)
            {
                var (op, ow, oh) = PngIo.LoadGray8(entry.OpacityPath, size);
                PngIo.SaveGray8(paths.Opacity, op, ow, oh);
                opacityPath = paths.Opacity;
            }

            File.WriteAllText(paths.Sidecar, BakeSidecar.Build(
                size,
                sources,
                sourceNormalConvention,
                normalConventionDeclared,
                aoBakedIntoBaseColor,
                f0Path is not null,
                opacityPath is not null).ToJson());
            log?.Invoke($"    -> Baked {size}px to {paths.Folder}{(isMetal ? " (metal: F0 written)" : string.Empty)}");

            return new BakedTextureSet(paths.BaseColor, paths.NormalGl, paths.Roughness, f0Path, opacityPath, WasSkipped: false);
        }

        private static List<(string path, long ticks)> CollectSources(SubstanceMaterialEntry entry)
        {
            var list = new List<(string, long)>();
            void Add(string? p) { if (p is not null) list.Add((p, File.GetLastWriteTimeUtc(p).Ticks)); }
            Add(entry.BaseColorPath);
            Add(entry.NormalPath);
            Add(entry.RoughnessPath);
            Add(entry.MetallicPath);
            Add(entry.AoPath);
            Add(entry.OpacityPath);
            return list;
        }

        private static bool OutputsExist(BakeOutputPaths p, BakeSidecar s)
        {
            return File.Exists(p.BaseColor) && File.Exists(p.NormalGl) && File.Exists(p.Roughness)
                && (!s.F0Written || File.Exists(p.F0))
                && (!s.OpacityWritten || File.Exists(p.Opacity));
        }
    }
}
