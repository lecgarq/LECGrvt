using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LECG.RevitCopilot.Llm;
using LECG.RevitCopilot.UI;

namespace LECG.RevitCopilot.SmokeTests;

internal static class PanelPresentationChecks
{
    internal static void Run(DockablePanel panel, List<object> results)
    {
        string output = Path.Combine(Path.GetDirectoryName(typeof(PanelPresentationChecks).Assembly.Location)!, "results", "panel-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(output);
        double oldWidth = panel.Width, oldHeight = panel.Height;
        var host = (StackPanel)panel.FindName("MessageHost");
        var previous = host.Children.Cast<UIElement>().ToArray();
        string oldProject = ((TextBlock)panel.FindName("ProjectNameText")).Text;
        var cancellationField = typeof(DockablePanel).GetField("_requestCancellation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object? oldCancellation = cancellationField.GetValue(panel);
        var observed = typeof(DockablePanel).GetField("_observedProject", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var displayed = typeof(DockablePanel).GetField("_displayedProject", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object? oldDisplayed = displayed.GetValue(panel);
        using var pending = new CancellationTokenSource();
        try
        {
            displayed.SetValue(panel, observed.GetValue(panel));
            cancellationField.SetValue(panel, pending);
            ((TextBlock)panel.FindName("ProjectNameText")).Text = "Guadalupe_Arquitectura.rvt";
            ((TextBlock)panel.FindName("ProjectSessionText")).Text = "Conversation saved · resumes when you reopen this file";
            ((TextBlock)panel.FindName("StatusText")).Text = "Ready";
            ((TextBlock)panel.FindName("ModelSummaryText")).Text = "GPT-6 Astra · low";
            ((TextBlock)panel.FindName("QuotaSummaryText")).Text = "Usage preview · example values only";
            Capture("welcome-340", 340, 760);
            var addMessage = typeof(DockablePanel).GetMethod("AddMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var kind = typeof(DockablePanel).GetNestedType("MessageKind", BindingFlags.NonPublic)!;
            host.Children.Clear();
            addMessage.Invoke(panel, ["You", "Inspect the project and list the linked Revit models. Do not modify anything.", Enum.Parse(kind, "User")]);
            addMessage.Invoke(panel, ["Copilot", "The project contains **6 linked models**.\n\n- Electrical\n- Facades\n- HVAC\n- Plumbing\n- Site\n- Structural\n\nNo model data was changed.", Enum.Parse(kind, "Assistant")]);
            Capture("conversation-480", 480, 900);
            Capture("conversation-340", 340, 760);
            using var data = JsonDocument.Parse("""{"questions":[{"id":"approve","question":"Allow lecg-revit.agent_apply to apply the reviewed removal of 6 Revit links?","options":[{"label":"Accept"},{"label":"Decline"}]}]}""");
            var request = CopilotQuestionRequest.Parse("preview", "item/tool/requestUserInput", data.RootElement)!;
            var card = (Border)typeof(DockablePanel).GetMethod("CreateQuestionCard", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [request])!;
            ((StackPanel)panel.FindName("ApprovalHost")).Children.Add(card);
            var body = (StackPanel)card.Child;
            var choice = body.Children.OfType<ComboBox>().Single();
            var buttons = body.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
            Require(choice.SelectedIndex == -1 && buttons.All(b => !b.IsDefault), "Approval has no preselected answer or default accept button.");
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!request.Completion.Task.IsCompleted, "Blank UI approval must not resolve.");
            Capture("approval-480", 480, 900);
            Capture("approval-340", 340, 760);
            choice.SelectedItem = "Decline";
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(request.Completion.Task.IsCompletedSuccessfully && JsonSerializer.Serialize(request.Completion.Task.Result).Contains("Decline"), "UI explicitly returns Decline.");
            ((StackPanel)panel.FindName("ApprovalHost")).Children.Remove(card);
            ((Expander)panel.FindName("SettingsExpander")).IsExpanded = true;
            Capture("settings-340", 340, 760);
            ((Expander)panel.FindName("SettingsExpander")).IsExpanded = false;
            panel.Visibility = Visibility.Collapsed;
            Require(pending.IsCancellationRequested, "Hiding the panel cancels a pending request.");
            panel.Visibility = Visibility.Visible;
            results.Add(new { test = "panel_project_history_approval_presentation", passed = true, snapshots = output, widths = new[] { 340, 480 }, live_ai_calls = 0 });
        }
        finally
        {
            panel.Visibility = Visibility.Visible;
            host.Children.Clear(); foreach (var item in previous) host.Children.Add(item);
            panel.Width = oldWidth; panel.Height = oldHeight;
            cancellationField.SetValue(panel, oldCancellation);
            displayed.SetValue(panel, oldDisplayed);
            ((TextBlock)panel.FindName("ProjectNameText")).Text = oldProject;
            ((StackPanel)panel.FindName("ApprovalHost")).Children.Clear();
            panel.UpdateLayout();
        }
        void Capture(string name, int width, int height)
        {
            panel.Width = width; panel.Height = height;
            panel.Measure(new Size(width, height)); panel.Arrange(new Rect(0,0,width,height)); panel.UpdateLayout();
            var send = (Button)panel.FindName("SendButton");
            Point location = send.TransformToAncestor(panel).Transform(new Point());
            Require(location.X >= 0 && location.X + send.ActualWidth <= width + 1 && location.Y + send.ActualHeight <= height + 1, "Send control stays within panel bounds.");
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
    }
    private static void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); }
}
