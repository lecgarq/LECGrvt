using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.UI;
using LECG.RevitCopilot.Llm;
using LECG.RevitCopilot.Revit;

namespace LECG.RevitCopilot.UI;

public partial class DockablePanel : UserControl, IDockablePaneProvider
{
    private readonly CodexAppServerClient _codexClient;
    private readonly LlmClient _directClient;
    private readonly ExternalEventDispatcher _dispatcher;
    private CancellationTokenSource? _requestCancellation;
    private bool _loadingModels;

    internal DockablePanel(
        CodexAppServerClient codexClient,
        LlmClient directClient,
        ExternalEventDispatcher dispatcher)
    {
        _codexClient = codexClient;
        _directClient = directClient;
        _dispatcher = dispatcher;
        InitializeComponent();
        InitializeUsageTracking();
        InitializeProjectConversations();
        Loaded += async (_, _) =>
        {
            if (ModelSelector.Items.Count == 0 && !_loadingModels) await LoadModelsAsync(false);
        };
        SetBusy(false);
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            MinimumWidth = 340
        };
    }

    private async void SendButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_requestCancellation is not null)
        {
            _requestCancellation.Cancel();
            _codexClient.CancelQuestions();
            StatusText.Text = "Cancelling...";
            return;
        }
        await SendCurrentPromptAsync();
    }

    private async void PromptInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await SendCurrentPromptAsync();
        }
    }

    private async Task SendCurrentPromptAsync()
    {
        string prompt = PromptInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || _requestCancellation is not null || _loadingModels || _switchingProject ||
            _displayedProject is null || _observedProject != _displayedProject) return;
        if (BackendSelector.SelectedIndex == 0)
        {
            if (ModelSelector.SelectedItem is not CodexModelOption model)
            {
                AddMessage("System", "Refresh the model list before sending.", MessageKind.Error);
                return;
            }
            _codexClient.SelectModel(model.Id, EffortSelector.SelectedItem as string);
        }

        PromptInput.Clear();
        AddMessage("You", prompt, MessageKind.User);
        SetBusy(true);
        _requestCancellation = new CancellationTokenSource();
        try
        {
            string response = await SelectedClient.RunAgentAsync(
                prompt,
                UpdateProgressAsync,
                _requestCancellation.Token);
            AddMessage("Copilot", response, MessageKind.Assistant);
        }
        catch (OperationCanceledException)
        {
            AddMessage("System", "The request was cancelled.", MessageKind.System);
        }
        catch (Exception ex)
        {
            AddMessage("System", ex.Message, MessageKind.Error);
        }
        finally
        {
            _requestCancellation.Dispose();
            _requestCancellation = null;
            SetBusy(false);
            StatusText.Text = "Ready";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 74, 71));
            if (BackendSelector.SelectedIndex == 0) RenderConversation();
            await SynchronizeProjectAsync();
            PromptInput.Focus();
            await RefreshUsageAsync();
        }
    }

    private IAgentClient SelectedClient => BackendSelector.SelectedIndex == 1
        ? _directClient
        : _codexClient;

    private void BackendSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        string message = BackendSelector.SelectedIndex == 1
            ? "Direct API mode selected. This uses provider API credits."
            : "Codex mode selected. This uses the ChatGPT account signed into the local Codex client.";
        AddMessage("System", message, MessageKind.System);
        SetBusy(false);
        UpdateUsageMode();
        RenderConversation();
        UpdateModelSummary();
        PromptInput.Focus();
    }

    private async void RefreshModels_OnClick(object sender, RoutedEventArgs e) => await LoadModelsAsync(false);

    private async void Reconnect_OnClick(object sender, RoutedEventArgs e) => await LoadModelsAsync(true);

    private void ModelSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EffortSelector is null || ModelSelector.SelectedItem is not CodexModelOption model) return;
        string? previous = EffortSelector.SelectedItem as string;
        EffortSelector.ItemsSource = model.Efforts;
        EffortSelector.SelectedItem = previous is not null && model.Efforts.Contains(previous)
            ? previous : model.Efforts.Contains("low") ? "low"
            : model.Efforts.Contains(model.DefaultEffort) ? model.DefaultEffort : model.Efforts.FirstOrDefault();
        UpdateModelSummary();
    }

    private async Task LoadModelsAsync(bool reconnect)
    {
        if (_loadingModels || _requestCancellation is not null) return;
        _loadingModels = true;
        SetBusy(false);
        StatusText.Text = reconnect ? "Reconnecting..." : "Loading models...";
        string? previousModel = (ModelSelector.SelectedItem as CodexModelOption)?.Id;
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
            if (reconnect)
            {
                await _codexClient.ReconnectAsync(timeout.Token);
                AddMessage("System", "Connection refreshed. Your project conversation is preserved.", MessageKind.System);
            }
            var models = await _codexClient.GetModelsAsync(timeout.Token);
            ModelSelector.ItemsSource = models;
            ModelSelector.SelectedItem = models.FirstOrDefault(m => m.Id == previousModel)
                ?? models.FirstOrDefault(m => m.IsDefault && !m.Hidden)
                ?? models.FirstOrDefault(m => !m.Hidden) ?? models[0];
            if (reconnect && _displayedProject is not null) await _codexClient.VerifyRevitToolsAsync(timeout.Token);
            StatusText.Text = "Ready";
            await RefreshUsageAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Connection needs attention";
            AddMessage("System", ex is OperationCanceledException
                ? "The connection timed out. Use Reconnect to retry." : ex.Message, MessageKind.Error);
        }
        finally
        {
            _loadingModels = false;
            SetBusy(false);
            await SynchronizeProjectAsync();
        }
    }

    private async void SelectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_requestCancellation is not null) return;
        SelectionButton.IsEnabled = false;
        StatusText.Text = "Reading selection...";
        try
        {
            string json = await _dispatcher.ExecuteToolAsync("__current_selection", "{}");
            using JsonDocument result = JsonDocument.Parse(json);
            JsonElement data = result.RootElement.GetProperty("data");
            long[] ids = data.GetProperty("element_ids")
                .EnumerateArray()
                .Select(value => value.GetInt64())
                .ToArray();

            if (ids.Length == 0)
            {
                AddMessage("System", "No elements are selected in Revit.", MessageKind.System);
            }
            else
            {
                string selectionText = $"Current Revit selection element IDs: [{string.Join(", ", ids)}]";
                PromptInput.Text = string.IsNullOrWhiteSpace(PromptInput.Text)
                    ? selectionText
                    : PromptInput.Text.TrimEnd() + Environment.NewLine + selectionText;
                PromptInput.CaretIndex = PromptInput.Text.Length;
                PromptInput.Focus();
            }
        }
        catch (Exception ex)
        {
            AddMessage("System", ex.Message, MessageKind.Error);
        }
        finally
        {
            SelectionButton.IsEnabled = true;
            StatusText.Text = "Ready";
        }
    }

    private Task UpdateProgressAsync(string status, string? detail)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = status;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(74, 74, 71));
            if (status == "Executing Revit API..." && !string.IsNullOrWhiteSpace(detail))
            {
                AddMessage("Tool", $"Running `{detail}` in Revit…", MessageKind.Tool);
            }
        }).Task;
    }

    private void SetBusy(bool busy)
    {
        bool codex = BackendSelector.SelectedIndex == 0;
        bool ready = !busy && !_loadingModels && !_switchingProject && _displayedProject is not null && _observedProject == _displayedProject;
        SendButton.IsEnabled = busy || (ready && (!codex || ModelSelector.SelectedItem is not null));
        SendButton.Content = busy ? "Stop" : "Send →";
        SelectionButton.IsEnabled = ready;
        NewConversationButton.IsEnabled = ready && codex;
        BackendSelector.IsEnabled = !busy && !_loadingModels;
        ModelSelector.IsEnabled = !busy && !_loadingModels && codex;
        EffortSelector.IsEnabled = !busy && !_loadingModels && codex && EffortSelector.Items.Count > 0;
        RefreshModelsButton.IsEnabled = !busy && !_loadingModels && codex;
        ReconnectButton.IsEnabled = !busy && !_loadingModels && codex;
        PromptInput.IsEnabled = ready;
    }

    private void AddMessage(string role, string content, MessageKind kind)
    {
        Color background = kind switch
        {
            MessageKind.User => Color.FromRgb(207, 224, 235),
            MessageKind.Assistant => Color.FromRgb(244, 243, 239),
            MessageKind.Tool => Color.FromRgb(221, 225, 211),
            MessageKind.Error => Color.FromRgb(235, 204, 201),
            _ => Color.FromRgb(228, 227, 222)
        };

        Border card = new()
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(Color.FromRgb(201, 200, 193)),
            BorderThickness = kind == MessageKind.Assistant ? new Thickness(0, 0, 0, 1) : new Thickness(0),
            CornerRadius = new CornerRadius(kind == MessageKind.Assistant ? 0 : 8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(kind == MessageKind.User ? 22 : 0, 0, 0, 14)
        };

        StackPanel body = new();
        body.Children.Add(new TextBlock
        {
            Text = role.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 74, 71)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        body.Children.Add(new RichTextBox
        {
            Document = MarkdownRenderer.CreateDocument(content),
            IsReadOnly = true,
            IsDocumentEnabled = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false
        });
        card.Child = body;
        MessageHost.Children.Add(card);
        WelcomeCard.Visibility = Visibility.Collapsed;
        ChatScroll.ScrollToEnd();
    }

    private enum MessageKind
    {
        User,
        Assistant,
        System,
        Tool,
        Error
    }
}
