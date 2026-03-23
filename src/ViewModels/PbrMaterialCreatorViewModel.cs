using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Models;
using LECG.Services.Interfaces;

namespace LECG.ViewModels
{
    public partial class PbrMaterialCreatorViewModel : BaseViewModel
    {
        private readonly IMaterialTextureLookupService? _textureLookup;

        public ObservableCollection<MaterialPageViewModel> MaterialPages { get; } = new();

        [ObservableProperty]
        private MaterialPageViewModel? _selectedPage;

        public bool CanRun => MaterialPages.Count > 0 && MaterialPages.All(p => p.CanRun);

        public bool CanRemovePage => MaterialPages.Count > 1;

        public PbrMaterialCreatorViewModel() : this(null) { }

        public PbrMaterialCreatorViewModel(IMaterialTextureLookupService? textureLookup)
        {
            _textureLookup = textureLookup;
            Title = "PBR MATERIAL CREATOR";
            MaterialPages.CollectionChanged += OnPagesCollectionChanged;
            AddPage();
        }

        [RelayCommand]
        private void AddPage()
        {
            var page = new MaterialPageViewModel(MaterialPages.Count + 1, _textureLookup);
            page.CanRunNotifier = () => OnPropertyChanged(nameof(CanRun));
            MaterialPages.Add(page);
            SelectedPage = page;
        }

        [RelayCommand]
        private void RemovePage(MaterialPageViewModel? page)
        {
            if (page == null || MaterialPages.Count <= 1)
            {
                return;
            }

            int index = MaterialPages.IndexOf(page);
            page.CanRunNotifier = null;
            MaterialPages.Remove(page);

            for (int i = 0; i < MaterialPages.Count; i++)
            {
                MaterialPages[i].PageNumber = i + 1;
            }

            if (MaterialPages.Count > 0)
            {
                SelectedPage = MaterialPages[Math.Min(index, MaterialPages.Count - 1)];
            }
        }

        public List<PbrMaterialCreateRequest> CreateRequests()
        {
            return MaterialPages.Select(p => p.CreateRequest()).ToList();
        }

        private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(CanRemovePage));
        }
    }
}
