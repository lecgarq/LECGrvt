using System.Windows;
using System.Windows.Threading;
using LECG.RevitCopilot.Llm;

namespace LECG.RevitCopilot.UI;

public partial class DockablePanel
{
    private readonly DispatcherTimer _usageTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool _refreshingUsage;
    private CodexUsageSnapshot? _lastUsage;
    private CancellationTokenSource? _usageCancellation;

    private void InitializeUsageTracking()
    {
        _usageTimer.Tick += async (_, _) => await RefreshUsageAsync();
        Loaded += (_, _) =>
        {
            _codexClient.UsageUpdated -= OnUsageUpdated;
            _codexClient.UsageUpdated += OnUsageUpdated;
            UpdateUsageMode();
        };
        Unloaded += (_, _) =>
        {
            _usageTimer.Stop();
            _usageCancellation?.Cancel();
            _codexClient.UsageUpdated -= OnUsageUpdated;
        };
        IsVisibleChanged += (_, _) =>
        {
            UpdateUsageMode();
            if (IsVisible) _ = RefreshUsageAsync();
        };
    }

    private void UpdateUsageMode()
    {
        bool active = IsLoaded && IsVisible && BackendSelector.SelectedIndex == 0;
        RefreshUsageButton.IsEnabled = active && !_refreshingUsage;
        if (active) _usageTimer.Start(); else _usageTimer.Stop();
        if (BackendSelector.SelectedIndex == 1)
        {
            QuotaText.Text = "Direct API usage is tracked by your provider, separately from Codex.";
            QuotaSummaryText.Text = "Direct API · separate provider billing";
            TokenUsageText.Text = "Subscription usage display paused.";
        }
        else if (_lastUsage is not null) RenderUsage(_lastUsage);
    }

    private void OnUsageUpdated(CodexUsageSnapshot snapshot)
    {
        // Never block the JSON-RPC reader waiting for the Revit UI thread.
        if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(() =>
        {
            _lastUsage = snapshot;
            if (IsLoaded && BackendSelector.SelectedIndex == 0) RenderUsage(snapshot);
        });
    }

    private void RenderUsage(CodexUsageSnapshot snapshot)
    {
        QuotaText.Text = snapshot.QuotaText;
        QuotaSummaryText.Text = snapshot.QuotaText.Split('\n')[0];
        QuotaSummaryText.ToolTip = snapshot.QuotaText;
        TokenUsageText.Text = snapshot.TokenText;
        UsageUpdatedText.Text = snapshot.UpdatedAt is { } time ? $"Updated {time:HH:mm:ss} · auto 60s" : "Not refreshed yet";
    }

    private async void RefreshUsage_OnClick(object sender, RoutedEventArgs e) => await RefreshUsageAsync();

    private async Task RefreshUsageAsync()
    {
        if (_refreshingUsage || !IsLoaded || !IsVisible || BackendSelector.SelectedIndex != 0 || ModelSelector.Items.Count == 0) return;
        _refreshingUsage = true;
        RefreshUsageButton.IsEnabled = false;
        _usageCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            _lastUsage = await _codexClient.ReadUsageAsync(_usageCancellation.Token);
            UsageUpdatedText.ToolTip = null;
            if (BackendSelector.SelectedIndex == 0) RenderUsage(_lastUsage);
        }
        catch (Exception ex)
        {
            UsageUpdatedText.Text = _lastUsage?.UpdatedAt is { } time
                ? $"Refresh failed · last quota update {time:HH:mm:ss}" : "Usage unavailable · retry Refresh usage";
            UsageUpdatedText.ToolTip = ex.Message;
        }
        finally
        {
            _usageCancellation.Dispose();
            _usageCancellation = null;
            _refreshingUsage = false;
            RefreshUsageButton.IsEnabled = IsLoaded && IsVisible && BackendSelector.SelectedIndex == 0;
        }
    }
}
