using System;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media;
using LECG.Utils;

namespace LECG.Core.Ribbon
{
    /// <summary>
    /// Factory for creating Ribbon elements in a consistent, safe manner.
    /// </summary>
    public static class RibbonFactory
    {
        /// <summary>
        /// Creates a PushButton on the specified panel using the provided configuration data.
        /// </summary>
        public static void CreateButton(RibbonPanel panel, RibbonButtonConfig data, string assemblyPath, string availabilityClassName = "")
        {
            if (panel == null || data == null || assemblyPath == null) return;

            try
            {
                PushButtonData buttonData = new PushButtonData(
                    data.Name, 
                    data.Text, 
                    assemblyPath, 
                    data.CommandClass);

                if (!string.IsNullOrEmpty(availabilityClassName))
                {
                    buttonData.AvailabilityClassName = availabilityClassName;
                }

                PushButton? button = panel.AddItem(buttonData) as PushButton;
                
                if (button != null)
                {
                    button.ToolTip = data.Tooltip;
                    
                    // Create icon 
                    button.LargeImage = data.Icon;
                    if (data.Icon16 != null) button.Image = data.Icon16;
                }
            }
            catch
            {
                // In a professional app, we might log this, but for UI creation we want to be resilient
                // System.Diagnostics.Debug.WriteLine($"Error creating button {data.Name}: {ex.Message}");
            }
        }
        /// <summary>
        /// Creates a PulldownButton on the specified panel.
        /// </summary>
        public static PulldownButton CreatePulldownButton(RibbonPanel panel, string name, string text, string tooltip, ImageSource? icon, ImageSource? icon16 = null)
        {
            if (panel == null || name == null || text == null || tooltip == null) return null!;

            try
            {
                PulldownButtonData data = new PulldownButtonData(name, text);
                PulldownButton? button = panel.AddItem(data) as PulldownButton;

                if (button != null)
                {
                    button.ToolTip = tooltip;
                    if (icon != null) button.LargeImage = icon;
                    if (icon16 != null) button.Image = icon16;
                }
                return button!;
            }
            catch
            {
                return null!;
            }
        }

        public static void AddItemToPulldown(PulldownButton pulldown, RibbonButtonConfig data, string assemblyPath, string availabilityClassName = "")
        {
            if (pulldown == null || data == null || assemblyPath == null) return;

            try
            {
                PushButtonData buttonData = new PushButtonData(
                    data.Name,
                    data.Text,
                    assemblyPath,
                    data.CommandClass);

                // Add Availability if specified
                if (!string.IsNullOrEmpty(availabilityClassName))
                {
                    buttonData.AvailabilityClassName = availabilityClassName;
                }

                PushButton? button = pulldown.AddPushButton(buttonData);
                if (button != null)
                {
                    button.ToolTip = data.Tooltip;
                    // Use LargeImage for pulldown items usually? Or Image? 
                    // Revit API docs say LargeImage is used for the icon in the list if accessible.
                    if (data.Icon != null) button.LargeImage = data.Icon;
                    if (data.Icon16 != null) button.Image = data.Icon16;
                }
            }
            catch { }
        }
    }
}
