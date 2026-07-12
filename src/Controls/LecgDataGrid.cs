using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LECG.Controls
{
    public class LecgDataGrid : DataGrid
    {
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
    }
}
