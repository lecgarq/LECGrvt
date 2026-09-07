using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LECG.RevitCopilot.Llm;

namespace LECG.RevitCopilot.UI;

public partial class DockablePanel
{
    private readonly Dictionary<string, Border> _approvalCards = [];

    private void ShowQuestion(CopilotQuestionRequest request)
    {
        if (Dispatcher.HasShutdownStarted) { request.Completion.TrySetResult(request.CancelResponse); return; }
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_requestCancellation is null || _requestCancellation.IsCancellationRequested || _observedProject != _displayedProject || !IsVisible)
            { request.Completion.TrySetResult(request.CancelResponse); return; }
            var card = CreateQuestionCard(request);
            _approvalCards[request.Id] = card;
            ApprovalHost.Children.Add(card);
            StatusText.Text = "Your decision needed";
        });
    }

    private Border CreateQuestionCard(CopilotQuestionRequest request)
    {
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = request.Title.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#7A5F48") });
            body.Children.Add(new TextBlock { Text = "Review before continuing", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,6,0,5) });
            body.Children.Add(new TextBlock { Text = "Nothing is approved automatically. Model changes still require the final Revit confirmation.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0,0,0,10) });
            var inputs = new Dictionary<string, FrameworkElement>();
            foreach (var question in request.Questions)
            {
                body.Children.Add(new TextBlock { Text = question.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,6) });
                FrameworkElement input = question.Options.Length > 0
                    ? new ComboBox { ItemsSource = question.Options, SelectedIndex = -1, ToolTip = "Choose explicitly; no answer is preselected" }
                    : new TextBox { TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxLength = 4000, MinHeight = 50, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                System.Windows.Automation.AutomationProperties.SetName(input, question.Text);
                inputs.Add(question.Id, input);
                body.Children.Add(input);
            }
            var validation = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brush("#C53030"), Margin = new Thickness(0,6,0,0) };
            body.Children.Add(validation);
            var actions = new WrapPanel { Margin = new Thickness(0,10,0,0) };
            var cancel = new Button { Content = "Decline / dismiss", IsDefault = false, Margin = new Thickness(0,0,8,0) };
            var submit = new Button { Content = "Submit choice", IsDefault = false, Background = Brush("#1E4257"), Foreground = Brush("#F4F3EF") };
            cancel.Click += (_, _) => { request.Completion.TrySetResult(request.CancelResponse); cancel.IsEnabled = submit.IsEnabled = false; };
            submit.Click += (_, _) =>
            {
                if (_observedProject != _displayedProject || _requestCancellation?.IsCancellationRequested != false)
                { request.Completion.TrySetResult(request.CancelResponse); return; }
                var answers = inputs.ToDictionary(p => p.Key, p => p.Value is ComboBox combo ? combo.SelectedItem as string ?? "" : ((TextBox)p.Value).Text.Trim());
                if (request.Submit(answers)) cancel.IsEnabled = submit.IsEnabled = false;
                else validation.Text = "Choose or enter an answer for each required question.";
            };
            actions.Children.Add(cancel); actions.Children.Add(submit); body.Children.Add(actions);
            return new Border { Child = body, Background = Brush("#E5DCCE"), BorderBrush = Brush("#B99A72"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(12), Margin = new Thickness(0,6,0,4) };
    }

    private void CloseQuestion(string id)
    {
        if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(() =>
        {
            if (_approvalCards.Remove(id, out var card)) ApprovalHost.Children.Remove(card);
        });
    }

    private static Brush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
}
