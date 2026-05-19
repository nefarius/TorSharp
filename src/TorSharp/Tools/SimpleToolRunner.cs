using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Knapcode.TorSharp.Tools;

internal class SimpleToolRunner : IToolRunner
{
    private readonly ConcurrentBag<Process> _processes = new();

    public event EventHandler<DataEventArgs>? Stdout;
    public event EventHandler<DataEventArgs>? Stderr;

    public Task StartAsync(Tool tool)
    {
        var arguments = string.Join(" ", tool.Settings.GetArguments(tool));
        var environmentVariables = tool.Settings.GetEnvironmentVariables(tool);
        var startInfo = new ProcessStartInfo
        {
            FileName = tool.ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = tool.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        foreach (var pair in environmentVariables)
        {
            startInfo.EnvironmentVariables[pair.Key] = pair.Value;
        }

        var process = Process.Start(startInfo)
            ?? throw new TorSharpException($"Unable to start the process '{tool.ExecutablePath}'.");

        process.OutputDataReceived += (_, e) =>
        {
            Stdout?.Invoke(this, new DataEventArgs(tool.ExecutablePath, e.Data));
        };

        process.ErrorDataReceived += (_, e) =>
        {
            Stderr?.Invoke(this, new DataEventArgs(tool.ExecutablePath, e.Data));
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes.Add(process);

        return Task.CompletedTask;
    }

    public void Stop()
    {
        while (!_processes.IsEmpty)
        {
            if (_processes.TryTake(out var process))
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        if (process.CloseMainWindow())
                        {
                            process.WaitForExit(1000);
                        }
                    }

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                }
            }
        }
    }

    public void Dispose() => Stop();
}
