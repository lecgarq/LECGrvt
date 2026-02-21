using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using LECG.Core;
using LECG.Utils;

namespace LECG.Views.Base
{
    public class LecgWindow : Window
    {
        public ICommand CloseCommand { get; }
        public ICommand MinimizeCommand { get; }

        // Dependency Property for Icon
        public static readonly DependencyProperty WindowIconProperty =
            DependencyProperty.Register("WindowIcon", typeof(Geometry), typeof(LecgWindow), new PropertyMetadata(null));

        public Geometry WindowIcon
        {
            get { return (Geometry)GetValue(WindowIconProperty); }
            set { SetValue(WindowIconProperty, value); }
        }

        public LecgWindow()
        {
            // Set Default Icon if not set
            if (WindowIcon == null)
            {
                WindowIcon = Icons.Home;
            }

            // Init Commands
            CloseCommand = new RelayCommand(() => Close());
            MinimizeCommand = new RelayCommand(() => WindowState = WindowState.Minimized);

            // Behavior fixes for WindowStyle=None
            this.Loaded += (s, e) => CenterOnParent();
        }

        private void CenterOnParent()
        {
            // If we have an owner, center on it
            if (this.Owner != null)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2;
                this.Top = this.Owner.Top + (this.Owner.Height - this.ActualHeight) / 2;
            }
            else
            {
                // Fallback to primary screen center if no owner set
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
    }
}
