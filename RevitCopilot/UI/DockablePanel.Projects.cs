using System.Windows;
using System.Windows.Controls;
using LECG.RevitCopilot.Models;

namespace LECG.RevitCopilot.UI;

public partial class DockablePanel
{
    private ProjectContext? _observedProject;
    private ProjectContext? _displayedProject;
    private bool _switchingProject;
    private int _visibleMessages = 60;

    private void InitializeProjectConversations()
    {
        _codexClient.QuestionRequested += ShowQuestion;
        _codexClient.QuestionClosed += CloseQuestion;
        _codexClient.SessionWarning += warning => Dispatcher.BeginInvoke(() =>
        {
            ProjectSessionText.Text = "History save failed · keep Revit open";
            AddMessage("History", warning, MessageKind.Error);
        });
        _codexClient.ConversationChanged += () => Dispatcher.BeginInvoke(() =>
        {
            UpdateProjectLabel();
        });
        Unloaded += (_, _) => { _requestCancellation?.Cancel(); _codexClient.CancelQuestions(); };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) { _requestCancellation?.Cancel(); _codexClient.CancelQuestions(); }
        };
    }

    internal void ObserveProject(ProjectContext? project)
    {
        if (_observedProject == project) return;
        _observedProject = project;
        _requestCancellation?.Cancel();
        _codexClient.CancelQuestions();
        PromptInput.Clear();
        ProjectNameText.Text = project?.FileName ?? "Open a Revit project";
        ProjectNameText.ToolTip = project?.Location;
        ProjectSessionText.Text = project is null ? "No active model · tools paused" : "Loading this project's conversation…";
        SetBusy(_requestCancellation is not null);
        if (IsLoaded) _ = SynchronizeProjectAsync();
    }

    private async Task SynchronizeProjectAsync()
    {
        if (_switchingProject || _loadingModels || _requestCancellation is not null || !IsLoaded) return;
        _switchingProject = true;
        SetBusy(false);
        try
        {
            while (_displayedProject != _observedProject)
            {
                var target = _observedProject;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                await _codexClient.SelectProjectAsync(target, timeout.Token);
                _directClient.ResetProjectConversation(target);
                _displayedProject = target;
                _visibleMessages = 60;
                RestoreModelPreference();
                RenderConversation();
                UpdateProjectLabel();
            }
        }
        catch (Exception ex)
        {
            _displayedProject = null;
            ProjectSessionText.Text = "History unavailable · use Reconnect to retry";
            MessageHost.Children.Clear();
            AddMessage("Connection", ex.Message, MessageKind.Error);
        }
        finally { _switchingProject = false; SetBusy(false); }
    }

    private void RestoreModelPreference()
    {
        var model = ModelSelector.Items.Cast<Llm.CodexModelOption>().FirstOrDefault(m => m.Id == _codexClient.SavedModel);
        if (model is not null) ModelSelector.SelectedItem = model;
        if (_codexClient.SavedEffort is { } effort && EffortSelector.Items.Contains(effort)) EffortSelector.SelectedItem = effort;
        if (ModelSelector.SelectedItem is Llm.CodexModelOption selected)
            _codexClient.SelectModel(selected.Id, EffortSelector.SelectedItem as string);
    }

    private void UpdateProjectLabel()
    {
        if (_displayedProject is null || _observedProject != _displayedProject) return;
        ProjectSessionText.Text = !_displayedProject.Persistent ? _displayedProject.Location :
            _codexClient.ConversationThreadId is null ? "New conversation · saved with this project" : "Conversation saved · resumes when you reopen this file";
    }

    private void RenderConversation()
    {
        var history = (BackendSelector.SelectedIndex == 0 ? _codexClient.ConversationHistory : _directClient.History)
            .Where(m => m.Role is "user" or "assistant" or "system").ToArray();
        MessageHost.Children.Clear();
        WelcomeCard.Visibility = history.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        OlderMessagesButton.Visibility = history.Length > _visibleMessages ? Visibility.Visible : Visibility.Collapsed;
        foreach (var message in history.TakeLast(_visibleMessages))
            AddMessage(message.Role == "user" ? "You" : message.Role == "assistant" ? "Copilot" : "Session",
                message.Content, message.Role == "user" ? MessageKind.User : message.Role == "assistant" ? MessageKind.Assistant : MessageKind.System);
    }

    private void OlderMessages_OnClick(object sender, RoutedEventArgs e)
    {
        _visibleMessages += 60;
        RenderConversation();
        ChatScroll.ScrollToTop();
    }

    private async void NewConversation_OnClick(object sender, RoutedEventArgs e)
    {
        if (_requestCancellation is not null || _switchingProject || _displayedProject is null) return;
        if (MessageBox.Show($"Start a new conversation for {_displayedProject.FileName}?\n\nThe existing conversation remains saved in Codex and in local history.",
                "New project conversation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _switchingProject = true;
        SetBusy(false);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _codexClient.NewConversationAsync(timeout.Token);
            _visibleMessages = 60;
            RenderConversation();
            UpdateProjectLabel();
        }
        catch (Exception ex) { AddMessage("Session", ex.Message, MessageKind.Error); }
        finally { _switchingProject = false; SetBusy(false); await SynchronizeProjectAsync(); }
    }

    private void EffortSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateModelSummary();
    private void UpdateModelSummary()
    {
        if (ModelSummaryText is null || BackendSelector is null) return;
        ModelSummaryText.Text = BackendSelector.SelectedIndex == 1 ? "Direct API · settings" :
            ModelSelector?.SelectedItem is Llm.CodexModelOption model ? $"{model.Id} · {EffortSelector?.SelectedItem ?? "default"}" : "Model & connection";
    }

    private void InspectProject_OnClick(object sender, RoutedEventArgs e) => SetSuggestedPrompt("Inspect the active project and view. Summarize it briefly and list up to 10 walls. Do not modify anything.");
    private void ExploreTools_OnClick(object sender, RoutedEventArgs e) => SetSuggestedPrompt("Find native tools for checking this project's model quality. Suggest three useful checks without modifying anything.");
    private void SetSuggestedPrompt(string prompt)
    {
        if (!PromptInput.IsEnabled) return;
        PromptInput.Text = prompt;
        PromptInput.Focus();
    }
}
