using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LECG.ViewModels.Components;
using Autodesk.Revit.DB;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Core;
using LECG.Core.Naming;
using Microsoft.Win32;
using System.IO;

namespace LECG.ViewModels
{
    public partial class ConvertCadViewModel : BaseViewModel
    {
        private readonly ICadConversionService _service;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private string _newFamilyName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private string _templatePath = "";

        [ObservableProperty]
        private string _lineStyleName = "CAD_Style";

        [ObservableProperty]
        private System.Windows.Media.Color _lineColor = System.Windows.Media.Colors.Black;

        [ObservableProperty]
        private int _lineWeight = 1;

        public List<ColorItem> AvailableColors { get; } = new List<ColorItem>
        {
            new ColorItem("Black", System.Windows.Media.Colors.Black),
            new ColorItem("Red", System.Windows.Media.Colors.Red),
            new ColorItem("Green", System.Windows.Media.Colors.Green),
            new ColorItem("Blue", System.Windows.Media.Colors.Blue),
            new ColorItem("Orange", System.Windows.Media.Colors.Orange),
            new ColorItem("Dark Gray", System.Windows.Media.Colors.DarkGray),
            new ColorItem("Gray", System.Windows.Media.Colors.Gray),
            new ColorItem("Magenta", System.Windows.Media.Colors.Magenta),
            new ColorItem("Cyan", System.Windows.Media.Colors.Cyan)
        };

        [ObservableProperty]
        private ColorItem? _selectedColorItem;

        partial void OnSelectedColorItemChanged(ColorItem? value)
        {
            if (value != null) LineColor = value.Color;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        [NotifyPropertyChangedFor(nameof(IsSelectionMode))]
        [NotifyPropertyChangedFor(nameof(IsFileMode))]
        [NotifyPropertyChangedFor(nameof(SelectionVisibility))]
        [NotifyPropertyChangedFor(nameof(FileVisibility))]
        private bool _useSelectedImport = true;

        public bool IsSelectionMode => UseSelectedImport;
        public bool IsFileMode 
        { 
            get => !UseSelectedImport;
            set => UseSelectedImport = !value;
        }
        
        public System.Windows.Visibility SelectionVisibility => UseSelectedImport ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility FileVisibility => !UseSelectedImport ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRun))]
        private string _dwgFilePath = "";

        [ObservableProperty]
        private double _progress;

        public int[] AvailableWeights { get; } = Enumerable.Range(1, 16).ToArray();

        public SelectionViewModel Selection { get; } = new SelectionViewModel();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isFinished;

        public System.Collections.ObjectModel.ObservableCollection<string> Logs { get; } = new System.Collections.ObjectModel.ObservableCollection<string>();

        public Action? RunOperation { get; set; }
        public Action? PlaceOperation { get; set; }

        public bool ShouldPlace { get; private set; }
        public ElementId? CreatedFamilySymbolId { get; set; }

        public bool CanRun => 
            !IsBusy && !IsFinished &&
            (UseSelectedImport ? Selection.HasSelection : !string.IsNullOrWhiteSpace(DwgFilePath)) 
            && !string.IsNullOrWhiteSpace(NewFamilyName) 
            && !string.IsNullOrWhiteSpace(TemplatePath);

        public ConvertCadViewModel(ICadConversionService service)
        {
            _service = service;
            Title = "CAD TO DETAIL ITEM";
            Selection.ElementName = "Imported CAD";
            TemplatePath = _service.GetDefaultTemplatePath();
            Selection.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionViewModel.HasSelection)) OnPropertyChanged(nameof(CanRun)); };
            SelectedColorItem = AvailableColors[0];
            LineWeight = 1;
            AddLog("System initialized.");
        }

        public void AddLog(string msg)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() => {
                Logs.Add($"[{System.DateTime.Now:HH:mm:ss}] {msg}");
                StatusMessage = msg;
            }));
        }

        [RelayCommand]
        private void BrowseDwg()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CAD Files (*.dwg)|*.dwg|All files (*.*)|*.*",
                Title = "Select CAD File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                DwgFilePath = openFileDialog.FileName;
                if (string.IsNullOrWhiteSpace(NewFamilyName))
                {
                    NewFamilyName = Path.GetFileNameWithoutExtension(DwgFilePath) + "_Detail";
                }
            }
        }

        public ElementId? SelectedElementId { get; private set; }

        public void SetSelection(Element e)
        {
            ArgumentNullException.ThrowIfNull(e);

            SelectedElementId = e.Id;
            Selection.UpdateSelection(1);
            
            if (e != null)
            {
                ElementId typeId = e.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element? type = e.Document.GetElement(typeId);
                    if (type != null)
                    {
                        NewFamilyName = DetailFamilyNamePolicy.FromTypeName(type.Name);
                    }
                }
            }
        }

        [RelayCommand]
        private void BrowseTemplate()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "Select Detail Item Template",
                InitialDirectory = Path.GetDirectoryName(TemplatePath)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TemplatePath = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private void Run()
        {
             if (CanRun)
             {
                 IsBusy = true;
                 OnPropertyChanged(nameof(CanRun));
                 RunOperation?.Invoke();
             }
        }

        [RelayCommand]
        private void Place()
        {
            if (IsFinished && CreatedFamilySymbolId != null)
            {
                PlaceOperation?.Invoke();
            }
        }

        [RelayCommand]
        private void Close()
        {
            CloseAction?.Invoke();
        }
    }

    public class ColorItem
    {
        public string Name { get; }
        public System.Windows.Media.Color Color { get; }
        public System.Windows.Media.SolidColorBrush Brush { get; }

        public ColorItem(string name, System.Windows.Media.Color color)
        {
            Name = name;
            Color = color;
            Brush = new System.Windows.Media.SolidColorBrush(color);
        }
    }
}
