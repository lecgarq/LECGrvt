using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// UiSnapshot — renders every LECG window to PNG without Revit, so a theme change can be seen.
//
//   dotnet build tools/UiSnapshot -p:SkipRevitDeploy=true
//   tools/UiSnapshot/bin/x64/Debug/net8.0-windows/UiSnapshot.exe src/Views <out-dir> [name-filter]
//
// How: each view's XAML is loaded as loose XAML through the built LECG assembly — x:Class and
// event handlers stripped, clr-namespaces re-targeted at LECG — then shown, measured and drawn.
// DataContext is null, so bound text is empty and Visibility bindings show everything; what
// you see is layout and style, not data. "OVERFLOW +n" flags content taller than the window
// (n includes the root margin, so ~48 is zero; scrollable views over-report by design).
// The base LecgWindow shares one saved-size file across runs; it is deleted afterwards.
static class Program
{
    static readonly string[] Events = { "Click","Loaded","Unloaded","Closing","Closed","SelectionChanged","TextChanged","Checked","Unchecked",
        "MouseDoubleClick","KeyDown","KeyUp","PreviewKeyDown","PreviewMouseDown","PreviewMouseLeftButtonDown","MouseLeftButtonDown","MouseLeftButtonUp",
        "Drop","DragOver","DragEnter","DragLeave","SizeChanged","GotFocus","LostFocus","ValueChanged","Expanded","Collapsed","Initialized","ContentRendered",
        "PreviewTextInput","MouseEnter","MouseLeave","Sorting","CellEditEnding","RowEditEnding","PreviewMouseWheel","ScrollChanged","MouseMove","MouseDown","MouseUp",
        "PreviewMouseUp","PreviewMouseMove","DataContextChanged","IsVisibleChanged","Activated","Deactivated","StateChanged","LocationChanged","SourceInitialized","RequestBringIntoView","SelectedItemChanged","ItemContainerGenerator" };

    [STAThread]
    static int Main(string[] args)
    {
        var viewsDir = args[0]; var outDir = args[1]; var filter = args.Length > 2 ? args[2] : null;
        var revit = @"C:\Program Files\Autodesk\Revit 2026";
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            var name = new AssemblyName(e.Name).Name + ".dll"; var p = Path.Combine(revit, name);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        var lecg = typeof(LECG.Views.Base.LecgWindow).Assembly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Directory.CreateDirectory(outDir);
        var theme = new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/LECG;component/src/Resources/Themes/LecgTheme.xaml") };
        var heading = (FontFamily)theme["FontFamilyHeading"];
        var glyphs = heading.GetTypefaces().Select(t => t.TryGetGlyphTypeface(out var g) ? g.FamilyNames.Values.First() + "/" + g.Weight : "unresolved").ToArray();
        Console.WriteLine("FontFamilyHeading=" + heading.Source + " -> [" + string.Join(", ", glyphs) + "]");
        // Embedded fallbacks must resolve on their own, not only behind a system face this machine happens to have.
        foreach (var probe in new[] { "./#Jost*", "./#Poppins" })
        {
            var fam = new FontFamily(new Uri("pack://application:,,,/LECG;component/src/Resources/Fonts/"), probe);
            var faces = fam.GetTypefaces().Select(t => t.TryGetGlyphTypeface(out var g) ? g.Weight.ToString() : "unresolved").ToArray();
            Console.WriteLine($"embedded {probe} -> [{string.Join(", ", faces)}]");
        }
        var body = (FontFamily)theme["FontFamilyBody"];
        Console.WriteLine("FontFamilyBody=" + body.Source + " -> " + (body.GetTypefaces().Any(t => t.TryGetGlyphTypeface(out _)) ? "resolves" : "UNRESOLVED"));

        var evRx = new Regex(@"\s(?:" + string.Join("|", Events) + @")=""[A-Za-z_][A-Za-z0-9_]*""");
        var files = Directory.GetFiles(viewsDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Components")).Where(f => filter is null || Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase));
        int fail = 0;
        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            try
            {
                var xaml = File.ReadAllText(f);
                if (!xaml.Contains("<base:LecgWindow")) continue;
                xaml = Regex.Replace(xaml, @"\s+x:Class=""[^""]*""", "");
                xaml = Regex.Replace(xaml, @"clr-namespace:(LECG(?:\.[A-Za-z0-9_]+)*)""", "clr-namespace:$1;assembly=LECG\"");
                xaml = evRx.Replace(xaml, "");
                var ctx = new ParserContext { BaseUri = new Uri(Path.GetFullPath(f)) };
                var win = (Window)XamlReader.Parse(xaml, ctx);
                win.WindowStartupLocation = WindowStartupLocation.Manual; win.Left = 0; win.Top = 0;
                var rootTag = xaml.Substring(0, xaml.IndexOf('>'));
                var mw = Regex.Match(rootTag, @"\sWidth=""(\d+)"""); var mh = Regex.Match(rootTag, @"\sHeight=""(\d+)""");
                win.Show();
                if (mw.Success) win.Width = double.Parse(mw.Groups[1].Value); if (mh.Success) win.Height = double.Parse(mh.Groups[1].Value);
                if (!mw.Success && !mh.Success) { }
                app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                win.UpdateLayout();
                var w = (int)Math.Ceiling(win.ActualWidth); var h = (int)Math.Ceiling(win.ActualHeight);
                var root = (FrameworkElement)win.Content;
                var rtb = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen()) { dc.DrawRectangle(win.Background ?? Brushes.White, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight)); dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight)); }
                rtb.Render(dv);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(Path.Combine(outDir, name + ".png")); enc.Save(fs);
                root.Measure(new Size(root.ActualWidth, double.PositiveInfinity));
                var need = root.DesiredSize.Height; var have = root.ActualHeight;
                Console.WriteLine($"ok   {name} {w}x{h} content needs {need:F0} of {have:F0}{(need > have + 1 ? $"  OVERFLOW +{need - have:F0}" : "")}");
                root.Measure(new Size(root.ActualWidth, root.ActualHeight));
                win.Close();
            }
            catch (Exception ex) { fail++; Console.WriteLine($"FAIL {name}: {ex.GetBaseException().Message}"); }
        }
        app.Shutdown();
        var stray = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LECG", "Settings", "LecgWindow_Settings.json");
        if (File.Exists(stray)) File.Delete(stray);
        return fail;
    }
}
