using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Knapcode.TorSharp.Tools;

internal sealed class CliWrapToolRunner : IToolRunner
{
    private readonly ConcurrentBag<RunningTool> _running = new();

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
        _running.Add(new RunningTool(task, forcefulCts, gracefulCts));

        // Fire-and-forget: the process runs independently until Stop() is called.
        return Task.CompletedTask;
    }

    public void Stop()
    {
        while (_running.TryTake(out var r))
        {
            r.GracefulCts.Cancel();
            if (!r.Task.Task.Wait(TimeSpan.FromSeconds(1)))
            {
                r.ForcefulCts.Cancel();
            }

            try
            {
                r.Task.GetAwaiter().GetResult();
            }
            catch
            {
                // OperationCanceledException and non-zero exit codes are expected on shutdown.
            }

            r.GracefulCts.Dispose();
            r.ForcefulCts.Dispose();
        }
    }

    public void Dispose() => Stop();

    private sealed class RunningTool
    {
        public RunningTool(CommandTask<CommandResult> task, CancellationTokenSource forcefulCts, CancellationTokenSource gracefulCts)
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
