using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Autodesk.Revit.UI;

namespace LECG.RevitCopilot.Revit;

internal sealed class ExternalEventDispatcher : IExternalEventHandler, IDisposable
{
    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private readonly SemaphoreSlim _raiseGate = new(1, 1);
    private readonly ToolExecutor _executor;
    private ExternalEvent? _externalEvent;
    private bool _disposed;

    internal ExternalEventDispatcher(ToolExecutor executor)
    {
        _executor = executor;
    }

    internal void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _externalEvent ??= ExternalEvent.Create(this);
    }

    internal Task<string> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default,
        string? expectedProjectKey = null,
        string? expectedRuntimeKey = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_externalEvent is null)
        {
            throw new InvalidOperationException("The Revit external event has not been initialized.");
        }

        WorkItem item = new(toolName, argumentsJson, cancellationToken, expectedProjectKey, expectedRuntimeKey);
        _queue.Enqueue(item);
        _ = EnsureRaisedAsync(item, cancellationToken);
        return item.Completion.Task;
    }

    public void Execute(UIApplication app)
    {
        while (_queue.TryDequeue(out WorkItem? item))
        {
            if (!item.TryStart())
            {
                item.Dispose();
                continue;
            }

            try
            {
                double queueWaitMs = Stopwatch.GetElapsedTime(item.EnqueuedAt).TotalMilliseconds;
                if (!string.IsNullOrEmpty(item.ExpectedProjectKey) || !string.IsNullOrEmpty(item.ExpectedRuntimeKey))
                {
                    var doc = app.ActiveUIDocument?.Document ?? throw new InvalidOperationException("The conversation's project is no longer active.");
                    var current = ProjectContextReader.Read(doc);
                    if (current.Key != item.ExpectedProjectKey || current.RuntimeKey != item.ExpectedRuntimeKey)
                        throw new InvalidOperationException("The active project changed. This request was blocked; return to the conversation's project and generate fresh inputs/previews.");
                }
                string result = _executor.Execute(app, item.ToolName, item.ArgumentsJson);
                JsonObject response = JsonNode.Parse(result)!.AsObject();
                response["queue_wait_ms"] = queueWaitMs;
                item.Completion.TrySetResult(response.ToJsonString());
            }
            catch (Exception ex)
            {
                item.Completion.TrySetException(ex);
            }
            finally
            {
                item.Dispose();
            }
        }
    }

    public string GetName() => "LECG Revit Copilot API dispatcher";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_queue.TryDequeue(out WorkItem? item))
        {
            item.Completion.TrySetException(new ObjectDisposedException(nameof(ExternalEventDispatcher)));
            item.Dispose();
        }

        _externalEvent?.Dispose();
        _raiseGate.Dispose();
    }

    private async Task EnsureRaisedAsync(WorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            await _raiseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (!item.Completion.Task.IsCompleted)
                {
                    ExternalEventRequest request = _externalEvent!.Raise();
                    if (request == ExternalEventRequest.Accepted) return;
                    if (request is ExternalEventRequest.Denied or ExternalEventRequest.TimedOut)
                    {
                        item.Reject(
                            new InvalidOperationException($"Revit rejected the external event request: {request}."));
                        return;
                    }

                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _raiseGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            item.Cancel();
        }
        catch (Exception ex)
        {
            item.Reject(ex);
        }
    }

    private sealed class WorkItem : IDisposable
    {
        private readonly CancellationTokenRegistration _registration;
        private int _state;

        internal WorkItem(string toolName, string argumentsJson, CancellationToken cancellationToken, string? projectKey, string? runtimeKey)
        {
            ExpectedProjectKey = projectKey;
            ExpectedRuntimeKey = runtimeKey;
            ToolName = toolName;
            ArgumentsJson = argumentsJson;
            Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _registration = cancellationToken.Register(
                static state => ((WorkItem)state!).Cancel(),
                this);
        }

        internal string ToolName { get; }
        internal string? ExpectedProjectKey { get; }
        internal string? ExpectedRuntimeKey { get; }
        internal long EnqueuedAt { get; } = Stopwatch.GetTimestamp();
        internal string ArgumentsJson { get; }
        internal TaskCompletionSource<string> Completion { get; }

        internal bool TryStart()
        {
            return !Completion.Task.IsCompleted
                && Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        }

        internal void Cancel()
        {
            if (Interlocked.CompareExchange(ref _state, -1, 0) == 0)
            {
                Completion.TrySetCanceled();
            }
        }

        internal void Reject(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _state, -1, 0) == 0)
            {
                Completion.TrySetException(exception);
            }
        }

        public void Dispose() => _registration.Dispose();
    }
}
