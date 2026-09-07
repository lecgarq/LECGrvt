using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Utilities;
using LECG.Models;
using LECG.Services;
using LECG.ViewModels;

namespace LECG.Views.Base
{
    public class LecgWindow : Window
    {
        protected UIDocument? UiDocument { get; private set; }

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

        private const string ThemeUri =
            "pack://application:,,,/LECG;component/src/Resources/Themes/LecgTheme.xaml";

        // Loaded once and shared across windows. Scoped to LECG windows on purpose: merging this
        // into Application.Current.Resources would push its implicit Button/TextBox/ComboBox styles
        // onto Revit's own dialogs and every other add-in in the process.
        private static readonly ResourceDictionary? SharedTheme = LoadTheme();

        private static ResourceDictionary? LoadTheme()
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(ThemeUri, UriKind.Absolute) };
            }
            catch (Exception ex)
            {
                // A missing theme must never stop a window from opening — the startup error
                // dialog is itself a LecgWindow, and swallowing it would hide real failures.
                System.Diagnostics.Debug.WriteLine($"[LecgWindow] Failed to load theme: {ex.Message}");
                return null;
            }
        }

        private void ApplyTheme()
        {
            if (SharedTheme is null) return;

            // Most views also merge LecgTheme in their own XAML Resources; those merges are
            // applied after this one and simply win. Adding it here guarantees every window
            // is themed, including views that do not declare it (HomeView, SearchReplaceView)
            // and any future one.
            Resources.MergedDictionaries.Add(SharedTheme);

            // Brand ground and type for the whole window. These are inherited properties, so
            // every TextBlock, control and template inside picks them up — including views that
            // replace Resources in XAML, because property values survive that replacement.
            SetResourceReference(BackgroundProperty, "LecgBaseBackground");
            SetResourceReference(ForegroundProperty, "LecgTextPrimary");
            SetResourceReference(FontFamilyProperty, "FontFamilyBody");
            SetResourceReference(FontSizeProperty, "FontSizeBody");
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
        }

        public LecgWindow()
        {
            ApplyTheme();

            // Set Default Icon if not set
            if (WindowIcon == null)
            {
                WindowIcon = Icons.Home;
            }

            // Init Commands
            CloseCommand = new LocalRelayCommand(() => Close());
            MinimizeCommand = new LocalRelayCommand(() => WindowState = WindowState.Minimized);

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
            try
            {
                WindowInteropHelper helper = new WindowInteropHelper(this);
                if (helper.Owner == IntPtr.Zero)
                {
                    helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                }
            }
            catch (InvalidOperationException)
            {
                // Owner cannot be set after dialog is shown — safe to ignore
            }
            LoadWindowState();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            base.OnClosing(e);
        }

        public virtual void Initialize(UIDocument uiDoc)
        {
            UiDocument = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
        }

        protected void BindDialogClose(BaseViewModel viewModel, Func<bool> shouldRun)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(shouldRun);

            viewModel.CloseAction = () =>
            {
                try
                {
                    DialogResult = shouldRun();
                }
                catch (InvalidOperationException)
                {
                    // DialogResult can only be set on a window shown via ShowDialog().
                    // If this fails, force the result via a tag and close.
                    Tag = shouldRun() ? "DialogOK" : null;
                    Close();
                }
            };
        }

        protected void BindAcceptedClose(BaseViewModel viewModel)
        {
            BindDialogClose(viewModel, static () => true);
        }

        protected void BindClose(BaseViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            viewModel.CloseAction = () => Close();
        }

        private string GetSettingsFileName() => $"{this.GetType().Name}_Settings.json";

        private void LoadWindowState()
        {
            try
            {
                var settings = SettingsManager.Load<WindowSettings>(GetSettingsFileName());
                if (settings != null && settings.IsInitialized)
                {
                    if (!TryNormalizeWindowSettings(settings, out WindowSettings normalized))
                    {
                        return;
                    }

                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = normalized.Left;
                    this.Top = normalized.Top;
                    this.Width = normalized.Width;
                    this.Height = normalized.Height;
                    this.WindowState = normalized.State;

                    EnsureVisible();
                }
            }
            catch (Exception ex) when (IsExpectedWindowStateException(ex))
            {
            }
        }

        private bool TryNormalizeWindowSettings(WindowSettings settings, out WindowSettings normalized)
        {
            normalized = settings;

            double minWidth = Math.Max(this.MinWidth, 300);
            double minHeight = Math.Max(this.MinHeight, 180);
            double maxWidth = Math.Max(minWidth, SystemParameters.VirtualScreenWidth);
            double maxHeight = Math.Max(minHeight, SystemParameters.VirtualScreenHeight);

            if (!double.IsFinite(settings.Width) || !double.IsFinite(settings.Height))
            {
                return false;
            }

            normalized.Width = Math.Min(Math.Max(settings.Width, minWidth), maxWidth);
            normalized.Height = Math.Min(Math.Max(settings.Height, minHeight), maxHeight);

            if (!double.IsFinite(settings.Left) || !double.IsFinite(settings.Top))
            {
                normalized.Left = (SystemParameters.VirtualScreenLeft + (SystemParameters.VirtualScreenWidth - normalized.Width) / 2);
                normalized.Top = (SystemParameters.VirtualScreenTop + (SystemParameters.VirtualScreenHeight - normalized.Height) / 2);
            }

            return true;
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
            catch (Exception ex) when (IsExpectedWindowStateException(ex))
            {
            }
        }

        private static bool IsExpectedWindowStateException(Exception ex)
        {
            return ex is InvalidOperationException
                or NotSupportedException
                or XamlParseException
                or System.IO.IOException
                or UnauthorizedAccessException;
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

        private class LocalRelayCommand : ICommand
        {
            private readonly Action _execute;
            public LocalRelayCommand(Action execute) => _execute = execute;
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute();
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}
