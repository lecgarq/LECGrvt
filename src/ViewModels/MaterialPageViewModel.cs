using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;
using Microsoft.Win32;

namespace LECG.ViewModels
{
    public partial class MaterialPageViewModel : ObservableObject
    {
        private readonly IMaterialTextureLookupService? _textureLookup;

        [ObservableProperty]
        private string _materialName = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _materialClass = string.Empty;

        [ObservableProperty]
        private string _folderPath = string.Empty;

        [ObservableProperty]
        private string _diffusePath = string.Empty;

        [ObservableProperty]
        private string _roughnessPath = string.Empty;

        [ObservableProperty]
        private string _normalPath = string.Empty;

        [ObservableProperty]
        private string _metallicPath = string.Empty;

        [ObservableProperty]
        private string _aoPath = string.Empty;

        [ObservableProperty]
        private string _opacityPath = string.Empty;

        [ObservableProperty]
        private BitmapImage? _diffusePreview;

        [ObservableProperty]
        private BitmapImage? _roughnessPreview;

        [ObservableProperty]
        private BitmapImage? _normalPreview;

        [ObservableProperty]
        private BitmapImage? _metallicPreview;

        [ObservableProperty]
        private BitmapImage? _aoPreview;

        [ObservableProperty]
        private BitmapImage? _opacityPreview;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int _detectedCount;

        public int BatchMaterialCount { get; private set; }

        public string TabHeader => $"Material {PageNumber}";

        public bool CanRun =>
            !string.IsNullOrWhiteSpace(MaterialName) &&
            !string.IsNullOrWhiteSpace(DiffusePath) &&
            File.Exists(DiffusePath) &&
            !string.IsNullOrWhiteSpace(NormalPath) &&
            File.Exists(NormalPath) &&
            !string.IsNullOrWhiteSpace(RoughnessPath) &&
            File.Exists(RoughnessPath) &&
            !string.IsNullOrWhiteSpace(MetallicPath) &&
            File.Exists(MetallicPath) &&
            HasValidOptionalPath(AoPath) &&
            HasValidOptionalPath(OpacityPath);

        public Action? CanRunNotifier { get; set; }

        public static IReadOnlyList<string> DefaultMaterialClasses { get; } = new[]
        {
            "Ceramic", "Concrete", "Earth", "Fabric", "Gas", "Generic",
            "Glass", "Liquid", "Masonry", "Metal", "Organic", "Paint",
            "Plastic", "Stone", "Wood"
        };

        public MaterialPageViewModel(int pageNumber) : this(pageNumber, null) { }

        public MaterialPageViewModel(int pageNumber, IMaterialTextureLookupService? textureLookup)
        {
            _pageNumber = pageNumber;
            _textureLookup = textureLookup;
        }

        partial void OnMaterialNameChanged(string value)
        {
            NotifyCanRun();
        }

        partial void OnFolderPathChanged(string value)
        {
            bool exists = !string.IsNullOrWhiteSpace(value) && Directory.Exists(value);
            BatchMaterialCount = exists ? SubstanceLibraryScanner.Scan(value).Entries.Count : 0;
            if (exists && BatchMaterialCount == 0)
            {
                ScanFolderForTextures(value);
            }
            NotifyCanRun();
        }

        partial void OnDiffusePathChanged(string value)
        {
            DiffusePreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnRoughnessPathChanged(string value)
        {
            RoughnessPreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnNormalPathChanged(string value)
        {
            NormalPreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnMetallicPathChanged(string value)
        {
            MetallicPreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnAoPathChanged(string value)
        {
            AoPreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnOpacityPathChanged(string value)
        {
            OpacityPreview = LoadPreview(value);
            NotifyCanRun();
        }

        partial void OnPageNumberChanged(int value) => OnPropertyChanged(nameof(TabHeader));

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select PBR texture folder"
            };

            if (dialog.ShowDialog() == true)
            {
                FolderPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void Browse(string target)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.exr;*.hdr|All Files|*.*",
                Multiselect = false,
                CheckFileExists = true,
                Title = $"Select {target} texture"
            };

            if (!string.IsNullOrWhiteSpace(FolderPath) && Directory.Exists(FolderPath))
            {
                dialog.InitialDirectory = FolderPath;
            }

            if (dialog.ShowDialog() != true) return;

            SetPathByTarget(target, dialog.FileName);
        }

        [RelayCommand]
        private void ClearPath(string target)
        {
            SetPathByTarget(target, string.Empty);
        }

        private void SetPathByTarget(string target, string value)
        {
            switch (target)
            {
                case "Diffuse": DiffusePath = value; break;
                case "Roughness": RoughnessPath = value; break;
                case "Normal": NormalPath = value; break;
                case "Metallic": MetallicPath = value; break;
                case "AO": AoPath = value; break;
                case "Opacity": OpacityPath = value; break;
            }
        }

        private void ScanFolderForTextures(string folder)
        {
            if (_textureLookup == null) return;

            Dictionary<string, string?> detected = _textureLookup.ScanFolder(folder);
            int count = 0;

            if (detected.TryGetValue("Diffuse", out string? diffuse) && diffuse != null)
            { DiffusePath = diffuse; count++; }

            if (detected.TryGetValue("Roughness", out string? roughness) && roughness != null)
            { RoughnessPath = roughness; count++; }

            if (detected.TryGetValue("Normal", out string? normal) && normal != null)
            { NormalPath = normal; count++; }

            if (detected.TryGetValue("Metallic", out string? metallic) && metallic != null)
            { MetallicPath = metallic; count++; }

            if (detected.TryGetValue("AO", out string? ao) && ao != null)
            { AoPath = ao; count++; }

            if (detected.TryGetValue("Opacity", out string? opacity) && opacity != null)
            { OpacityPath = opacity; count++; }

            DetectedCount = count;
        }

        public PbrMaterialCreateRequest CreateRequest()
        {
            return new PbrMaterialCreateRequest(
                MaterialName.Trim(),
                MaterialName.Trim(),
                string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                string.IsNullOrWhiteSpace(MaterialClass) ? null : MaterialClass.Trim(),
                DiffusePath.Trim(),
                NormalizeOptionalPath(RoughnessPath),
                NormalizeOptionalPath(NormalPath),
                NormalizeOptionalPath(MetallicPath),
                NormalizeOptionalPath(AoPath),
                NormalizeOptionalPath(OpacityPath),
                true,
                2500,
                2500,
                0,
                0,
                0,
                true);
        }

        private void NotifyCanRun()
        {
            OnPropertyChanged(nameof(CanRun));
            CanRunNotifier?.Invoke();
        }

        private static BitmapImage? LoadPreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.DecodePixelWidth = 64;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasValidOptionalPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) || File.Exists(path);
        }

        private static string? NormalizeOptionalPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }
    }
}
