using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Events;
using LECG.RevitCopilot.Configuration;
using LECG.RevitCopilot.Llm;
using LECG.RevitCopilot.Mcp;
using LECG.RevitCopilot.Revit;
using LECG.RevitCopilot.UI;

namespace LECG.RevitCopilot;

public sealed class RevitCopilotApp : IExternalApplication
{
    public static readonly DockablePaneId PaneId = new(
        new Guid("7A8C8C84-8D7D-4E66-8CE5-F31C3B2294E1"));

    internal static ExternalEventDispatcher? Dispatcher { get; private set; }
    internal static LlmClient? Client { get; private set; }
    internal static CodexAppServerClient? CodexClient { get; private set; }
    internal static McpBridgeService? McpBridge { get; private set; }
    internal static DockablePanel? Panel { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        try
        {
            CopilotConfiguration configuration = new();
            configuration.Initialize();

            Dispatcher = new ExternalEventDispatcher(new ToolExecutor());
            Dispatcher.Initialize();
            application.ControlledApplication.DocumentChanged += OnDocumentChanged;
            McpBridge = new McpBridgeService(Dispatcher);
            McpBridge.Start();
            Client = new LlmClient(configuration, Dispatcher);
            CodexClient = new CodexAppServerClient(configuration);

            DockablePanel panel = new(CodexClient, Client, Dispatcher);
            Panel = panel;
            application.Idling += OnIdling;
            application.RegisterDockablePane(PaneId, "LECG AI Copilot", panel);
            RegisterRibbonButton(application);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            application.Idling -= OnIdling;
            application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            CodexClient?.Dispose();
            McpBridge?.Dispose();
            Client?.Dispose();
            Dispatcher?.Dispose();
            CodexClient = null;
            Panel = null;
            McpBridge = null;
            Client = null;
            Dispatcher = null;
            TaskDialog.Show("LECG Revit Copilot", $"Startup failed:\n\n{ex}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Idling -= OnIdling;
        application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
        CodexClient?.Dispose();
        McpBridge?.Dispose();
        Client?.Dispose();
        Dispatcher?.Dispose();
        CodexClient = null;
        Panel = null;
        McpBridge = null;
        Client = null;
        Dispatcher = null;
        return Result.Succeeded;
    }

    private static void OnDocumentChanged(object? sender, DocumentChangedEventArgs e) => ToolExecutor.NotifyDocumentChanged();

    private static void OnIdling(object? sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
    {
        if (sender is not UIApplication app || Panel is null) return;
        try { Panel.ObserveProject(app.ActiveUIDocument?.Document is { } doc ? ProjectContextReader.Read(doc) : null); }
        catch { Panel.ObserveProject(null); } // Unknown identity is never an actionable model.
    }

    private static void RegisterRibbonButton(UIControlledApplication application)
    {
        const string panelName = "LECG Copilot";
        RibbonPanel panel = application.GetRibbonPanels()
            .FirstOrDefault(existing => string.Equals(existing.Name, panelName, StringComparison.Ordinal))
            ?? application.CreateRibbonPanel(panelName);

        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        PushButtonData buttonData = new(
            "LECG.RevitCopilot.Show",
            "AI\nCopilot",
            assemblyPath,
            typeof(RevitCopilotCommand).FullName!);
        buttonData.ToolTip = "Open the dockable LECG AI Copilot panel.";
        panel.AddItem(buttonData);
    }
}
