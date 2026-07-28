using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using LECG.Core.Warnings;
using LECG.Services;
using LECG.Views.Base;

namespace LECG.ViewModels
{
    public partial class WarningsViewModel : BaseViewModel
    {
        private readonly WarningsService _service;
        private UIDocument? _uidoc;

        public ObservableCollection<WarningGroup> Groups { get; } = new();

        public int TotalCount { get; private set; }
        public string Summary => TotalCount == 1 ? "1 warning" : $"{TotalCount} warnings";
        public bool HasWarnings => TotalCount > 0;

        public ICommand SelectCommand { get; }
        public ICommand ShowCommand { get; }
        public ICommand IsolateCommand { get; }

        public WarningsViewModel(WarningsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Title = "WARNINGS";
            SelectCommand = new RelayCommand<WarningGroup>(g => RunAction(g, _service.Select, "Select"));
            ShowCommand = new RelayCommand<WarningGroup>(g => RunAction(g, _service.Show, "Show"));
            IsolateCommand = new RelayCommand<WarningGroup>(g => RunAction(g, _service.Isolate, "Isolate"));
        }

        public void Initialize(UIDocument uidoc)
        {
            ArgumentNullException.ThrowIfNull(uidoc);
            _uidoc = uidoc;
            Load(_service.ReadWarnings(uidoc.Document));
        }

        // Revit-free seam: tests feed items directly.
        public void Load(IEnumerable<WarningItem> items)
        {
            Groups.Clear();
            foreach (WarningGroup group in WarningGroupingPolicy.Group(items))
            {
                Groups.Add(group);
            }
            TotalCount = Groups.Sum(g => g.Count);
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(HasWarnings));
        }

        private void RunAction(WarningGroup? group, Action<UIDocument, IEnumerable<long>> action, string actionName)
        {
            if (group == null || _uidoc == null) return;

            try
            {
                action(_uidoc, group.ElementIds);
            }
            catch (Exception ex)
            {
                // A throwing button inside a modal dialog must not take down Revit
                // (e.g. isolate while a non-graphical view is active).
                LecgDialog.Show($"{actionName} failed", ex.Message);
            }
        }
    }
}
