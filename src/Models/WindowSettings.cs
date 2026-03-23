using System.Windows;

namespace LECG.Models
{
    /// <summary>
    /// Model to store window position, size, and state for persistence.
    /// </summary>
    public class WindowSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState State { get; set; } = WindowState.Normal;

        /// <summary>
        /// Flag to check if the settings have been initialized.
        /// </summary>
        public bool IsInitialized { get; set; } = false;
    }
}
