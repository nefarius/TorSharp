using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Nefarius.Utilities.TorProxy.Tools;

internal sealed class CliWrapToolRunner : IToolRunner
{
    private readonly object _lock = new();
    private readonly List<RunningTool> _running = new();
    private bool _shutdownRequested;

    public event EventHandler<DataEventArgs>? Stdout;
    public event EventHandler<DataEventArgs>? Stderr;

    public Task StartAsync(Tool tool)
    {
        var environmentVariables = tool.Settings.GetEnvironmentVariables(tool)
            .ToDictionary(p => p.Key, p => (string?)p.Value);

        var cmd = Cli.Wrap(tool.ExecutablePath)
            .WithArguments(tool.Settings.GetArguments(tool), escape: false)
            .WithWorkingDirectory(tool.WorkingDirectory)
            .WithEnvironmentVariables(environmentVariables)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Stdout?.Invoke(this, new DataEventArgs(tool.ExecutablePath, line))))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Stderr?.Invoke(this, new DataEventArgs(tool.ExecutablePath, line))))
            .WithValidation(CommandResultValidation.None);

        var forcefulCts = new CancellationTokenSource();
        var gracefulCts = new CancellationTokenSource();
        var task = cmd.ExecuteAsync(forcefulCts.Token, gracefulCts.Token);
        var entry = new RunningTool(task, forcefulCts, gracefulCts);

        lock (_lock)
        {
            if (_shutdownRequested)
            {
                // Stop() was called concurrently; cancel the just-launched process immediately.
                CancelAndDispose(entry);
                throw new TorProxyException(
                    $"Cannot start '{tool.ExecutablePath}': the tool runner has already been stopped.");
            }

            _running.Add(entry);
        }

        // Fire-and-forget: the process runs independently until Stop() is called.
        return Task.CompletedTask;
    }

    public void Stop()
    {
        List<RunningTool> snapshot;
        lock (_lock)
        {
            if (_shutdownRequested)
            {
                return;
            }

            _shutdownRequested = true;
            snapshot = new List<RunningTool>(_running);
            _running.Clear();
        }

        List<Exception>? unexpected = null;
        foreach (var r in snapshot)
        {
            try
            {
                ShutdownOne(r);
            }
            catch (Exception ex)
            {
                (unexpected ??= new List<Exception>()).Add(ex);
            }
        }

        if (unexpected != null)
        {
            throw new AggregateException(
                "Unexpected exceptions occurred while stopping tool processes.", unexpected);
        }
    }

    public void Dispose() => Stop();

    private static void ShutdownOne(RunningTool r)
    {
        try
        {
            r.GracefulCts.Cancel();

            // Wait up to 1 s for graceful exit; swallow AggregateException in case the
            // task already faulted/was cancelled before we entered this wait.
            bool completed;
            try
            {
                completed = r.Task.Task.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                completed = true;
            }

            if (!completed)
            {
                r.ForcefulCts.Cancel();
            }

            try
            {
                if (!r.Task.Task.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TorProxyException("Timed out waiting for a tool process to exit after cancellation.");
                }

                r.Task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected: graceful or forceful cancellation on shutdown.
            }
            // Any other exception propagates to Stop(), which aggregates and rethrows.
        }
        finally
        {
            r.GracefulCts.Dispose();
            r.ForcefulCts.Dispose();
        }
    }

    // Used when a process is launched after Stop() has already been called.
    private static void CancelAndDispose(RunningTool r)
    {
        try
        {
            r.GracefulCts.Cancel();
            r.ForcefulCts.Cancel();
            try { r.Task.GetAwaiter().GetResult(); } catch { }
        }
        finally
        {
            r.GracefulCts.Dispose();
            r.ForcefulCts.Dispose();
        }
    }

    private sealed class RunningTool
    {
        public RunningTool(
            CommandTask<CommandResult> task,
            CancellationTokenSource forcefulCts,
            CancellationTokenSource gracefulCts)
        {
            Task = task;
            ForcefulCts = forcefulCts;
            GracefulCts = gracefulCts;
        }

        public CommandTask<CommandResult> Task { get; }
        public CancellationTokenSource ForcefulCts { get; }
        public CancellationTokenSource GracefulCts { get; }
    }
}
