using System.Windows.Media;

namespace LECG.Core.Ribbon
{
    /// <summary>
    /// Represents the configuration data for a single Ribbon PushButton.
    /// Renamed from RibbonItemData to avoid conflict with Revit API.
    /// </summary>
    public class RibbonButtonConfig
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public string CommandClass { get; set; }
        public string Tooltip { get; set; }
        public ImageSource Icon { get; set; }
        public ImageSource? Icon16 { get; set; }

        public RibbonButtonConfig(string name, string text, string commandClass, string tooltip, ImageSource icon, ImageSource? icon16 = null)
        {
            Name = name;
            Text = text;
            CommandClass = commandClass;
            Tooltip = tooltip;
            Icon = icon;
            Icon16 = icon16;
        }
    }
}
