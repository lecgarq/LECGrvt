using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.Services; // Assuming Logger is here or similar

namespace LECG.ViewModels
{
    public enum ViewSourceType
    {
        Views,
        ViewTemplates
    }

    public partial class FilterCopyViewModel : BaseViewModel
    {
        private readonly Document _doc;

        [ObservableProperty]
        private ObservableCollection<ViewContainer> _leftItems = new();

        [ObservableProperty]
        private ObservableCollection<ViewContainer> _rightItems = new();

        [ObservableProperty]
        private ViewSourceType _leftSourceType;

        [ObservableProperty]
        private ViewSourceType _rightSourceType;

        [ObservableProperty]
        private string _leftFilterText = string.Empty;

        [ObservableProperty]
        private string _rightFilterText = string.Empty;

        [ObservableProperty]
        private bool _isBusyLeft; // For loading indicator if needed

        public List<ViewSourceType> SourceTypes { get; } = new() { ViewSourceType.Views, ViewSourceType.ViewTemplates };

        public FilterCopyViewModel(Document doc)
        {
            _doc = doc;
            Title = "Filter Copy";
            LeftSourceType = ViewSourceType.Views;
            RightSourceType = ViewSourceType.Views;
            
            LoadData();
        }

        partial void OnLeftSourceTypeChanged(ViewSourceType value) => LoadLeftData();
        partial void OnRightSourceTypeChanged(ViewSourceType value) => LoadRightData();

        partial void OnLeftFilterTextChanged(string value) => FilterLeftItems();
        partial void OnRightFilterTextChanged(string value) => FilterRightItems();

        private void LoadData()
        {
            LoadLeftData();
            LoadRightData();
        }

        private void LoadLeftData()
        {
            LeftItems.Clear();
            var views = GetViews(LeftSourceType);
            foreach (var v in views)
            {
                var container = CreateViewContainer(v);
                if (container.Filters.Count > 0) // Only show views with filters on the left? User said "view templates or views, and filters for those templates or views"
                {
                   LeftItems.Add(container);
                }
            }
            FilterLeftItems();
        }

        private void LoadRightData()
        {
            RightItems.Clear();
            var views = GetViews(RightSourceType);
            foreach (var v in views)
            {
                RightItems.Add(CreateViewContainer(v));
            }
            FilterRightItems();
        }

        private List<View> GetViews(ViewSourceType type)
        {
            var collector = new FilteredElementCollector(_doc).OfClass(typeof(View));
            var views = new List<View>();

            foreach (View v in collector)
            {
                if (v.IsTemplate != (type == ViewSourceType.ViewTemplates)) continue;
                if (v.IsTemplate)
                {
                   views.Add(v);
                }
                else
                {
                    if (!v.IsTemplate && v.CanUseTemporaryVisibilityModes() && v.ViewType != ViewType.DrawingSheet && v.ViewType != ViewType.Internal && v.ViewType != ViewType.ProjectBrowser)
                    {
                        views.Add(v);
                    }
                }
            }
            return views.OrderBy(x => x.Name).ToList();
        }

        private ViewContainer CreateViewContainer(View v)
        {
            var container = new ViewContainer(v.Name, v.Id, v.IsTemplate) { ViewType = v.ViewType };
            
            // Get Filters
            var filterIds = v.GetOrderedFilters(); // Use ordered to respect priority
            foreach (var id in filterIds)
            {
                var filterElement = _doc.GetElement(id) as ParameterFilterElement;
                if (filterElement == null) continue;

                var overrides = v.GetFilterOverrides(id);
                var visibility = v.GetFilterVisibility(id); // Visibility setting

                container.Filters.Add(new FilterItem(filterElement.Name, id, overrides) 
                { 
                    IsVisible = visibility 
                });
            }
            return container;
        }

        private void FilterLeftItems()
        {
            ApplyFilter(LeftItems, LeftFilterText);
        }

        private void FilterRightItems()
        {
            ApplyFilter(RightItems, RightFilterText);
        }

        private void ApplyFilter(ObservableCollection<ViewContainer> items, string filterText)
        {
            var searchTerms = filterText?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToList() ?? new List<string>();

            if (!searchTerms.Any())
            {
                foreach (var view in items)
                {
                    view.IsVisible = true;
                    foreach (var filter in view.Filters)
                    {
                        filter.IsVisible = true;
                    }
                }
                return;
            }

            foreach (var view in items)
            {
                bool viewMatches = searchTerms.Any(term => view.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
                bool hasMatchingChildren = false;

                foreach (var filter in view.Filters)
                {
                    bool filterMatches = searchTerms.Any(term => filter.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
                    filter.IsVisible = filterMatches || viewMatches;
                    if (filterMatches) hasMatchingChildren = true;
                }

                view.IsVisible = viewMatches || hasMatchingChildren;
                if (hasMatchingChildren && !string.IsNullOrWhiteSpace(filterText))
                {
                    view.IsExpanded = true;
                }
            }
        }

        [RelayCommand]
        private void Transfer()
        {
            // Find selected filters in Left
            var selectedFilters = LeftItems.SelectMany(v => v.Filters).Where(f => f.IsSelected).ToList();
            if (selectedFilters.Count == 0) return;

            // Find selected views in Right
            var targetViews = RightItems.Where(x => x.IsSelected).ToList();
            
            if (targetViews.Count == 0) return;

            foreach (var target in targetViews)
            {
                foreach (var sourceFilter in selectedFilters)
                {
                    // Check if filter already exists in target
                    var existing = target.Filters.FirstOrDefault(f => f.Id == sourceFilter.Id);

                    if (existing != null)
                    {
                        // Update existing
                        existing.GraphicsSettings = sourceFilter.GraphicsSettings;
                        existing.IsVisible = sourceFilter.IsVisible;
                        if (existing.Status != FilterStatus.Created) // Don't overwrite Created with Modified
                        {
                            existing.Status = FilterStatus.Modified;
                        }
                    }
                    else
                    {
                        // Add new
                        target.Filters.Add(new FilterItem(sourceFilter.Name, sourceFilter.Id, sourceFilter.GraphicsSettings)
                        {
                            Status = FilterStatus.Created,
                            IsVisible = sourceFilter.IsVisible
                        });
                    }
                }
            }
        }

        [RelayCommand]
        private void ToggleRemove(FilterItem filter)
        {
            if (filter.Status == FilterStatus.Created)
            {
                // Remove from collection
                foreach(var view in RightItems)
                {
                    if (view.Filters.Contains(filter))
                    {
                        view.Filters.Remove(filter);
                        break;
                    }
                }
            }
            else
            {
                filter.Status = filter.Status == FilterStatus.Removable ? FilterStatus.Existing : FilterStatus.Removable;
            }
        }

        [RelayCommand]
        private void SetActiveView(string side)
        {
             var activeViewId = _doc.ActiveView.Id;
             var collection = side == "Left" ? LeftItems : RightItems;
             var found = collection.FirstOrDefault(x => x.Id == activeViewId);
             
             if (found != null)
             {
                 found.IsSelected = true;
                 found.IsExpanded = true;
             }
        }

        [RelayCommand]
        private void Bulk(string parameter)
        {
            // Format: "Side:Action" e.g. "Left:CheckAll"
            var parts = parameter.Split(':');
            if (parts.Length != 2) return;

            var side = parts[0];
            var action = parts[1];
            var collection = side == "Left" ? LeftItems : RightItems;

            foreach (var view in collection)
            {
                if (!view.IsVisible) continue;

                switch (action)
                {
                    case "Expand":
                        view.IsExpanded = true;
                        break;
                    case "Collapse":
                        view.IsExpanded = false;
                        break;
                    case "Check":
                        if (side == "Right") view.IsSelected = true;
                        foreach (var filter in view.Filters)
                        {
                            if (filter.IsVisible) filter.IsSelected = true;
                        }
                        break;
                    case "Uncheck":
                        if (side == "Right") view.IsSelected = false;
                        foreach (var filter in view.Filters)
                        {
                            if (filter.IsVisible) filter.IsSelected = false;
                        }
                        break;
                }
            }
        }

        protected override void Apply()
        {
            using (Transaction t = new Transaction(_doc, "Filter Copy"))
            {
                t.Start();
                try
                {
                    foreach (var viewContainer in RightItems)
                    {
                        var view = _doc.GetElement(viewContainer.Id) as View;
                        if (view == null) continue;

                        // Identify filters to process
                        var filtersToKeep = viewContainer.Filters.Where(f => f.Status != FilterStatus.Removable).ToList();
                        var filtersToRemove = viewContainer.Filters.Where(f => f.Status == FilterStatus.Removable).ToList();

                        // 1. Remove markers
                        foreach (var filter in filtersToRemove)
                        {
                            if (view.IsFilterApplied(filter.Id)) view.RemoveFilter(filter.Id);
                        }

                        // 2. Re-add in order
                        foreach (var filter in filtersToKeep)
                        {
                            if (view.IsFilterApplied(filter.Id)) view.RemoveFilter(filter.Id);
                            view.AddFilter(filter.Id);
                            view.SetFilterOverrides(filter.Id, filter.GraphicsSettings);
                            view.SetFilterVisibility(filter.Id, filter.IsVisible);
                        }
                    }
                    t.Commit();
                    CloseAction?.Invoke();
                }
                catch (Exception)
                {
                    t.RollBack();
                }
            }
        }
    }
}
