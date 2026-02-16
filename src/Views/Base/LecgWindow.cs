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
        }
    }
}
