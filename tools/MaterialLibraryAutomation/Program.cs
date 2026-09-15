using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Forms;

internal static class Program
{
    const uint BM_CLICK = 0x00F5;
    const int SW_RESTORE = 9;
    static readonly string Root = @"C:\LECG\MaterialLibrary";
    static readonly string LogPath = Path.Combine(Root, "material-browser-automation.log");

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.FirstOrDefault() is "--attach" or "--expand-home" or "--invoke-library-menu")
            {
                string attachedSnapshot = args.Length > 1 ? Path.GetFullPath(args[1])
                    : Path.Combine(Root, "material-browser-uia.json");
                var revit = Process.GetProcessesByName("Revit").Single();
                var attachedBrowser = Windows(revit.Id).First(w =>
                    w.Title.StartsWith("Material Browser", StringComparison.OrdinalIgnoreCase)).Handle;
                if (args[0] == "--expand-home")
                {
                    var browserRoot = AutomationElement.FromHandle(attachedBrowser)!;
                    var home = browserRoot.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.NameProperty, "Home"));
                    Require(home is not null, "Home library tree item is missing.");
                    Require(home!.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object pattern),
                        "Home library tree item cannot expand.");
                    ((ExpandCollapsePattern)pattern).Expand();
                    Thread.Sleep(1500);
                }
                if (args[0] == "--invoke-library-menu")
                {
                    var browserRoot = AutomationElement.FromHandle(attachedBrowser)!;
                    var buttons = browserRoot.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    var libraryMenu = buttons.Cast<AutomationElement>().Single(element =>
                        element.Current.AutomationId.Contains("mpViewToolBarFrame.QToolBar.Autodesk::UICore::DropDown")
                        && element.Current.BoundingRectangle.Y > 0);
                    Require(libraryMenu.TryGetCurrentPattern(InvokePattern.Pattern, out object invoke),
                        "Library menu button cannot invoke.");
                    ((InvokePattern)invoke).Invoke();
                    Thread.Sleep(750);
                    var menuItems = AutomationElement.RootElement.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem))
                        .Cast<AutomationElement>().Where(element => element.Current.ProcessId == revit.Id)
                        .Select(element => new { element.Current.Name, element.Current.AutomationId,
                            Bounds = element.Current.BoundingRectangle.ToString() }).ToArray();
                    File.WriteAllText(attachedSnapshot, JsonSerializer.Serialize(menuItems,
                        new JsonSerializerOptions { WriteIndented = true }));
                    Log($"library menu snapshot {attachedSnapshot} items={menuItems.Length}");
                    return 0;
                }
                WriteSnapshot(attachedBrowser, attachedSnapshot);
                return 0;
            }
            string model = args.Length > 0 ? Path.GetFullPath(args[0])
                : Path.Combine(Root, "LECG_PBR_Materials_Metric.rvt");
            string snapshot = args.Length > 1 ? Path.GetFullPath(args[1])
                : Path.Combine(Root, "material-browser-uia.json");
            Require(File.Exists(model), "Metric master does not exist.");
            string expectedModelTitle = Path.GetFileNameWithoutExtension(model);
            File.WriteAllText(LogPath, "");
            Log("launch " + model);
            var process = Process.Start(new ProcessStartInfo {
                FileName = @"C:\Program Files\Autodesk\Revit 2026\Revit.exe",
                ArgumentList = { model }, UseShellExecute = true })
                ?? throw new InvalidOperationException("Revit did not start.");

            IntPtr main = IntPtr.Zero;
            for (int i = 0; i < 300 && !process.HasExited; i++)
            {
                ClickUnsignedPrompts(process.Id);
                main = Windows(process.Id).FirstOrDefault(w =>
                    w.Title.StartsWith("Autodesk Revit", StringComparison.OrdinalIgnoreCase)
                    && w.Title.Contains(expectedModelTitle, StringComparison.OrdinalIgnoreCase)).Handle;
                if (main != IntPtr.Zero) break;
                Thread.Sleep(1000);
            }
            Require(!process.HasExited && main != IntPtr.Zero, "Metric master did not reach an interactive Revit window.");
            Log($"model ready hwnd={main}");
            ShowWindow(main, SW_RESTORE);
            Focus(main);
            Thread.Sleep(1000);
            SendKeys.SendWait("mt");
            Log("sent mt");

            IntPtr browser = IntPtr.Zero;
            for (int i = 0; i < 120 && !process.HasExited; i++)
            {
                browser = Windows(process.Id).FirstOrDefault(w =>
                    (w.Title.StartsWith("Material Browser", StringComparison.OrdinalIgnoreCase)
                    || w.Title.Equals("Materials", StringComparison.OrdinalIgnoreCase))
                    && w.Handle != main).Handle;
                if (browser != IntPtr.Zero) break;
                Thread.Sleep(500);
            }
            Require(browser != IntPtr.Zero, "Material Browser did not open.");
            Log($"browser ready hwnd={browser}");
            WriteSnapshot(browser, snapshot);
            return 0;
        }
        catch (Exception ex)
        {
            Log("ERROR " + ex);
            return 1;
        }
    }

    static void WriteSnapshot(IntPtr browser, string snapshot)
    {
        var root = AutomationElement.FromHandle(browser)
            ?? throw new InvalidOperationException("Material Browser has no UI Automation root.");
        var tree = Capture(root, 0, 16);
        File.WriteAllText(snapshot, JsonSerializer.Serialize(tree,
            new JsonSerializerOptions { WriteIndented = true }));
        Log("snapshot " + snapshot);
    }

    static Node Capture(AutomationElement element, int depth, int maxDepth)
    {
        var rect = element.Current.BoundingRectangle;
        var node = new Node(element.Current.Name, element.Current.ControlType.ProgrammaticName,
            element.Current.AutomationId, element.Current.ClassName, element.Current.IsEnabled,
            new[] { Finite(rect.X), Finite(rect.Y), Finite(rect.Width), Finite(rect.Height) }, new List<Node>());
        if (depth >= maxDepth) return node;
        var child = TreeWalker.RawViewWalker.GetFirstChild(element);
        while (child is not null)
        {
            try { node.Children.Add(Capture(child, depth + 1, maxDepth)); }
            catch (ElementNotAvailableException) { }
            child = TreeWalker.RawViewWalker.GetNextSibling(child);
        }
        return node;
    }

    static double Finite(double value) => double.IsFinite(value) ? value : 0;

    static void ClickUnsignedPrompts(int processId)
    {
        foreach (var window in Windows(processId).Where(w => w.Title == "Security - Unsigned Add-In"))
        {
            IntPtr button = IntPtr.Zero;
            EnumChildWindows(window.Handle, (child, _) => {
                if (Text(child) == "Always Load") button = child;
                return true;
            }, IntPtr.Zero);
            if (button != IntPtr.Zero)
            {
                SendMessage(button, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                Log("accepted unsigned local add-in");
            }
        }
    }

    static List<Window> Windows(int processId)
    {
        var result = new List<Window>();
        EnumWindows((handle, _) => {
            GetWindowThreadProcessId(handle, out uint owner);
            string title = Text(handle);
            if (owner == processId && title.Length > 0) result.Add(new Window(handle, title));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    static string Text(IntPtr handle)
    {
        var text = new StringBuilder(1024);
        GetWindowText(handle, text, text.Capacity);
        return text.ToString();
    }

    static void Log(string message)
    {
        string line = DateTime.UtcNow.ToString("O") + " " + message;
        File.AppendAllText(LogPath, line + Environment.NewLine);
        Console.WriteLine(line);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Focus(IntPtr handle)
    {
        uint targetThread = GetWindowThreadProcessId(handle, out _);
        uint currentThread = GetCurrentThreadId();
        bool attached = targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
        try
        {
            BringWindowToTop(handle);
            SetActiveWindow(handle);
            SetFocus(handle);
            SetForegroundWindow(handle);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, targetThread, false);
        }
        // Windows may report false even when the activation request succeeds. The
        // Material Browser appearance below is the authoritative focus check.
        Log($"focus requested foreground={GetForegroundWindow()}");
    }

    sealed record Node(string Name, string ControlType, string AutomationId, string ClassName,
        bool Enabled, double[] Bounds, List<Node> Children);
    readonly record struct Window(IntPtr Handle, string Title);
    delegate bool EnumDelegate(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumDelegate callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumDelegate callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr handle);
    [DllImport("user32.dll")] static extern IntPtr SetActiveWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr handle);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
}
