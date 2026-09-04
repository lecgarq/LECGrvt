using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core.Substance;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class SubstanceMaterialRowViewModel : ObservableObject
    {
        private readonly IMetallicProbeService? _probe;
        private bool? _isMetallic;

        public SubstanceMaterialEntry Entry { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private bool _existsInDocument;

        public SubstanceMaterialRowViewModel(SubstanceMaterialEntry entry, IMetallicProbeService? probe)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _probe = probe;
        }

        public string DisplayName => Entry.DisplayName;
        public string Category => Entry.Category;
        public bool HasBaseColor => true;
        public bool HasNormal => true;
        public bool HasRoughness => true;
        public bool HasAo => Entry.HasAo;
        public bool HasOpacity => Entry.HasOpacity;

        /// <summary>Lazy 64 px probe. Bound by the Maps column; computed on first access.</summary>
        public bool HasMetal
        {
            get
            {
                _isMetallic ??= _probe?.IsMetallic(Entry.MetallicPath) ?? false;
                return _isMetallic.Value;
            }
        }

        public string Status => ExistsInDocument ? "In doc" : string.Empty;

        public Action? SelectionChanged { get; set; }

        partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
        partial void OnExistsInDocumentChanged(bool value) => OnPropertyChanged(nameof(Status));

        public SubstanceRowState ToState() => new(Entry.Category, Entry.Slug, Entry.DisplayName, IsSelected);
    }
}
