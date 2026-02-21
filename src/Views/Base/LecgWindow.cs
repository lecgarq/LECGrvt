using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using LECG.Core;
using LECG.Utils;
using LECG.Models;
using LECG.Services;

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
            this.Loaded += (s, e) => 
            {
                // Only center if no saved position was applied
                if (this.WindowStartupLocation == WindowStartupLocation.CenterScreen)
                {
                    CenterOnParent();
                }
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            LoadWindowState();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            base.OnClosing(e);
        }

        private string GetSettingsFileName() => $"{this.GetType().Name}_Settings.json";

        private void LoadWindowState()
        {
            try
            {
                var settings = SettingsManager.Load<WindowSettings>(GetSettingsFileName());
                if (settings != null && settings.IsInitialized)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = settings.Left;
                    this.Top = settings.Top;
                    this.Width = settings.Width;
                    this.Height = settings.Height;
                    this.WindowState = settings.State;

                    EnsureVisible();
                }
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = new WindowSettings
                {
                    Left = this.Left,
                    Top = this.Top,
                    Width = this.ActualWidth,
                    Height = this.ActualHeight,
                    State = this.WindowState,
                    IsInitialized = true
                };

                // If minimized, don't save that as default state
                if (settings.State == WindowState.Minimized) settings.State = WindowState.Normal;

                SettingsManager.Save(settings, GetSettingsFileName());
            }
            catch { }
        }

        private void EnsureVisible()
        {
            if (this.Left + this.Width < SystemParameters.VirtualScreenLeft ||
                this.Left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ||
                this.Top + this.Height < SystemParameters.VirtualScreenTop ||
                this.Top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            {
                // Off-screen, reset to center
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                CenterOnParent();
            }
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
