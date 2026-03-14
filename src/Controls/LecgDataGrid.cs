using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LECG.Controls
{
    public class LecgDataGrid : DataGrid
    {
        public static readonly DependencyProperty CheckPropertyNameProperty =
            DependencyProperty.Register(
                nameof(CheckPropertyName),
                typeof(string),
                typeof(LecgDataGrid),
                new PropertyMetadata("IsChecked"));

        public string CheckPropertyName
        {
            get => (string)GetValue(CheckPropertyNameProperty);
            set => SetValue(CheckPropertyNameProperty, value);
        }

        public LecgDataGrid()
        {
            // Set base UI properties
            SetResourceReference(BackgroundProperty, "LecgBaseBackground");
            SetResourceReference(BorderBrushProperty, "LecgBorderLight");

            // Avoid arbitrary scaling and enforce sharp rendering
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            // Native Windows behaviors allowed
            SelectionMode = DataGridSelectionMode.Extended;
            
            // Standard performance flags for Large Revit Data
            EnableRowVirtualization = true;
            EnableColumnVirtualization = true;
            VirtualizingPanel.SetIsVirtualizing(this, true);
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);

            // Clean styling by default (could be extended later)
            AutoGenerateColumns = false;
            CanUserAddRows = false;
            CanUserDeleteRows = false;
            HeadersVisibility = DataGridHeadersVisibility.All;
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            RowHeaderWidth = 0;
            BorderThickness = new Thickness(1);
        }

        public void CheckAll() => SetAllBooleanProperty(true);
        public void UncheckAll() => SetAllBooleanProperty(false);
        
        // This could be expanded to select only the current filtered UI view
        private void SetAllBooleanProperty(bool value)
        {
            if (string.IsNullOrWhiteSpace(CheckPropertyName)) return;
            if (ItemsSource == null) return;

            var items = ItemsSource.Cast<object>().ToList();
            if (!items.Any()) return;

            // Reflect on the first element to get the property
            var targetType = items[0].GetType();
            var propInfo = targetType.GetProperty(CheckPropertyName, BindingFlags.Public | BindingFlags.Instance);
            
            if (propInfo == null || propInfo.PropertyType != typeof(bool)) return;

            foreach (var item in items)
            {
                propInfo.SetValue(item, value);
            }
        }
    }
}
